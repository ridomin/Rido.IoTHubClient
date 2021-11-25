using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace com_example
{
    public class thermostat_1
    {
        const string modelId = "dtmi:com:example:Thermostat;1";

        internal IMqttConnection _connection;

        int lastRid;
        public ConnectionSettings ConnectionSettings => _connection.ConnectionSettings;

        public Func<WritableProperty<double>, Task<WritableProperty<double>>> OnProperty_targetTemperature_Updated = null;
        public Func<Cmd_getMaxMinReport_Request, Task<Cmd_getMaxMinReport_Response>> OnCommand_getMaxMinReport_Invoked = null;

        public WritableProperty<double> Property_targetTemperature;

        ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        ConcurrentDictionary<int, TaskCompletionSource<int>> pendingUpdateTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<int>>();

        public thermostat_1(IMqttConnection c)
        {
             _connection = c;
           
        }

        public static async Task<thermostat_1> CreateDeviceClientAsync(string cs, CancellationToken cancellationToken)
        {
            async Task SubscribeToSysTopicsAsync(IMqttConnection connection)
            {
                var subres = await connection.SubscribeAsync(new string[] {
                                                    "$az/iot/methods/+/+",
                                                    "$az/iot/twin/get/response/+",
                                                    "$az/iot/twin/patch/response/+",
                                                    "$az/iot/twin/events/desired-changed/+" });

                subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            }

            ArgumentNullException.ThrowIfNull(cs);
            var connection = await HubMqttConnection.CreateAsync(new ConnectionSettings(cs) { ModelId = modelId }, cancellationToken);
            await SubscribeToSysTopicsAsync(connection);
            var client = new thermostat_1(connection);
            client.ConfigureSysTopicsCallbacks();
            return client;
        }

        public async Task InitTwinProperty_targetTemperature_Async(double defaultTargetTemp)
        {
            var twin = await GetTwinAsync();
            Property_targetTemperature = WritableProperty<double>.InitFromTwin(twin, "targetTemperature", defaultTargetTemp);
            var ack = await OnProperty_targetTemperature_Updated?.Invoke(Property_targetTemperature);
            _ = await UpdateTwinAsync(ack.ToAck());
        }

        private void ConfigureSysTopicsCallbacks()
        {
            _connection.OnMessage = async m =>
            {
                var topic = m.ApplicationMessage.Topic;
                var segments = topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (topic.Contains('?'))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["rid"]);
                    twinVersion = Convert.ToInt32(qs["v"]);
                }

                string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());

                if (topic.StartsWith("$az/iot/methods/getMaxMinReport"))
                {
                    Cmd_getMaxMinReport_Request req = new Cmd_getMaxMinReport_Request()
                    {
                        since = JsonSerializer.Deserialize<DateTime>(msg)
                    };
                    if (OnCommand_getMaxMinReport_Invoked != null)
                    {
                        var resp = await OnCommand_getMaxMinReport_Invoked.Invoke(req);
                        await _connection.PublishAsync($"$az/iot/methods/getMaxMinReport/response/?rid={rid}&rc={resp?._status}", resp);
                    }    
                }

                if (topic.StartsWith("$az/iot/twin/get/response"))
                {
                    if (pendingGetTwinRequests.TryRemove(rid, out TaskCompletionSource<string> tcs))
                    {
                        tcs.SetResult(msg);
                    }
                }

                if (topic.StartsWith("$az/iot/twin/patch/response"))
                {
                    if (pendingUpdateTwinRequests.TryRemove(rid, out TaskCompletionSource<int> tcs))
                    {
                        tcs.SetResult(twinVersion);
                    }
                }

                if (topic.StartsWith("$az/iot/twin/events/desired-changed"))
                {
                    JsonNode root = JsonNode.Parse(msg);
                    var ack = await Invoke_targetTemperature_Callback(root);
                    if (ack != null)
                    {
                        _ = UpdateTwinAsync(ack);
                    }
                }
            };
        }

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await _connection.PublishAsync($"$az/iot/twin/get/?rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingGetTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback?.ReasonCode}' publishing twin GET");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
        }

        public async Task<int> UpdateTwinAsync(object patch)
        {
            var tcs = new TaskCompletionSource<int>();
            var puback = await _connection.PublishAsync($"$az/iot/twin/patch/reported/?rid={lastRid++}",patch);
            if (puback.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingUpdateTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback.ReasonCode}' publishing twin PATCH");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
        }

        public async Task<int> Report_maxTempSinceLastReboot(double maxTempSinceLastReboot) => 
            await UpdateTwinAsync(new { maxTempSinceLastReboot });

        public async Task<MqttClientPublishResult> Send_temperature(double temperature)
        {
            return await _connection.PublishAsync($"$az/iot/telemetry", new { temperature });
        }

        private async Task<WritableProperty<double>> Invoke_targetTemperature_Callback(JsonNode desired)
        {
            if (desired?["targetTemperature"] != null)
            {
                var targetTemperatureProperty = new WritableProperty<double>("targetTemperature")
                {
                    Value = Convert.ToDouble(desired?["targetTemperature"]?.GetValue<double>()),
                    DesiredVersion = desired["$version"].GetValue<int>()
                };

                if (OnProperty_targetTemperature_Updated == null)
                {
                    return await Task.FromResult(targetTemperatureProperty);
                }
                else
                {
                    return await OnProperty_targetTemperature_Updated.Invoke(targetTemperatureProperty);
                }
            }
            else
            {
                return null;
            }    
        }
    }
}

