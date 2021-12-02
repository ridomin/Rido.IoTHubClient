﻿//  <auto-generated/> 
#nullable enable

using MQTTnet.Client.Publishing;
using pnp_memmon;
using Rido.IoTHubClient;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace dtmi_rido_pnp
{
    public class memmon
    {
        const string modelId = "dtmi:rido:pnp:memmon;1";
        internal IMqttConnection _connection;
        string initialTwin = string.Empty;
        internal int lastRid;

        //ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        ConcurrentDictionary<int, TaskCompletionSource<int>> pendingUpdateTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<int>>();

        public ConnectionSettings ConnectionSettings => _connection.ConnectionSettings;
        public Func<WritableProperty<bool>, Task<WritableProperty<bool>>>? OnProperty_enabled_Updated = null;
        public Func<WritableProperty<int>, Task<WritableProperty<int>>>? OnProperty_interval_Updated = null;
        public Func<Cmd_getRuntimeStats_Request, Task<Cmd_getRuntimeStats_Response>>? OnCommand_getRuntimeStats_Invoked = null;

        public WritableProperty<bool>? Property_enabled;
        public WritableProperty<int>? Property_interval;
        public DateTime Property_started { get; private set; }

        private BindRequestResponse getTwinBinder; 

        private memmon(IMqttConnection c)
        {
            _connection = c;
            ConfigureSysTopicsCallbacks(_connection);
            getTwinBinder = new BindRequestResponse(_connection, "$iothub/twin/GET/?$rid={0}", "$iothub/twin/res");
        }

        void ConfigureSysTopicsCallbacks(IMqttConnection connection)
        {
            connection.OnMessage = async m =>
            {
                var topic = m.ApplicationMessage.Topic;
                (int rid, int twinVersion) = TopicParser.ParseTopic(topic);
                string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());

                if (topic.StartsWith("$iothub/methods/POST/getRuntimeStats"))
                {
                    Cmd_getRuntimeStats_Request req = new Cmd_getRuntimeStats_Request()
                    {
                        DiagnosticsMode = JsonSerializer.Deserialize<DiagnosticsMode>(msg),
                        _rid = rid
                    };
                    if (OnCommand_getRuntimeStats_Invoked != null)
                    {
                        var resp = await OnCommand_getRuntimeStats_Invoked.Invoke(req);
                        _ = _connection.PublishAsync($"$iothub/methods/res/{resp?.Status}/?$rid={rid}", resp);
                    }
                }

                //if (topic.StartsWith("$iothub/twin/res/200"))
                //{
                //    if (pendingGetTwinRequests.TryRemove(rid, out var tcs))
                //    {
                //        tcs.SetResult(msg);
                //    }
                //}

                if (topic.StartsWith("$iothub/twin/res/204"))
                {
                    if (pendingUpdateTwinRequests.TryRemove(rid, out var tcs))
                    {
                        tcs.SetResult(twinVersion);
                    }
                }

                if (topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
                {
                    JsonNode? root = JsonNode.Parse(msg);
                    _ = Invoke_enabled_Callback(root);
                    _ = Invoke_interval_Callback(root);
                }
            };
        }

        public static async Task<memmon> CreateDeviceClientAsync(string cs, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(cs);
            var connection = await HubMqttConnection.CreateAsync(new ConnectionSettings(cs) { ModelId = modelId }, cancellationToken);
            var client = new memmon(connection);
            await SubscribeToSysTopicsAsync(connection);
            client.initialTwin = await client.GetTwinAsync();
            return client;
        }

        private static async Task SubscribeToSysTopicsAsync(IMqttConnection connection)
        {
            var subres = await connection.SubscribeAsync(new string[] {
                                                    "$iothub/methods/POST/#",
                                                    //"$iothub/twin/res/#",
                                                    "$iothub/twin/PATCH/properties/desired/#"});

            subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
        }

        public async Task InitProperty_enabled_Async(bool defaultEnabled)
        {
            Property_enabled = WritableProperty<bool>.InitFromTwin(initialTwin, "enabled", defaultEnabled);
            if (OnProperty_enabled_Updated != null && (Property_enabled.DesiredVersion > 1))
            {
                var ack = await OnProperty_enabled_Updated.Invoke(Property_enabled);
                _ = UpdateTwinAsync(ack.ToAck());
                Property_enabled = ack;
            }
            else
            {
                _ = UpdateTwinAsync(Property_enabled.ToAck());
            }
        }

        public async Task InitProperty_interval_Async(int defaultInterval)
        {
            Property_interval = WritableProperty<int>.InitFromTwin(initialTwin, "interval", defaultInterval);
            if (OnProperty_interval_Updated != null && (Property_interval.DesiredVersion > 1))
            {
                var ack = await OnProperty_interval_Updated.Invoke(Property_interval);
                _ = UpdateTwinAsync(ack.ToAck());
                Property_interval = ack;
            }
            else
            {
                _ = UpdateTwinAsync(Property_interval.ToAck());
            }
        }

       

        private async Task Invoke_interval_Callback(JsonNode? desired)
        {
            if (desired?["interval"] != null)
            {
                if (OnProperty_interval_Updated != null)
                {
                    var intervalProperty = new WritableProperty<int>("interval")
                    {
                        Value = Convert.ToInt32(desired?["interval"]?.GetValue<int>()),
                        Version = desired?["$version"]?.GetValue<int>() ?? 0
                    };
                    var ack = await OnProperty_interval_Updated.Invoke(intervalProperty);
                    if (ack != null)
                    {
                        Property_interval = ack;
                        _ = UpdateTwinAsync(ack.ToAck());
                    }
                }
            }
        }

        private async Task Invoke_enabled_Callback(JsonNode? desired)
        {
            if (desired?["enabled"] != null)
            {
                if (OnProperty_enabled_Updated != null)
                {
                    var enabledProperty = new WritableProperty<bool>("enabled")
                    {
                        Value = Convert.ToBoolean(desired?["enabled"]?.GetValue<bool>()),
                        Version = desired?["$version"]?.GetValue<int>() ?? 0,
                    };
                    var ack = await OnProperty_enabled_Updated.Invoke(enabledProperty);
                    if (ack != null)
                    {
                        Property_enabled = ack;
                        _ = UpdateTwinAsync(ack.ToAck());
                    }
                }
            }
        }

        public async Task<int> Report_started_Async(DateTime started) => await UpdateTwinAsync(new { started });
        
        public async Task<string> GetTwinAsync() => await getTwinBinder.SendRequestWaitForResponse(25);
        
        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await _connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingUpdateTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback?.ReasonCode}' publishing twin PATCH");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(15));
        }

        public async Task<MqttClientPublishResult> Send_workingSet_Async(double workingSet) => await Send_workingSet_Async(workingSet, CancellationToken.None);
        public async Task<MqttClientPublishResult> Send_workingSet_Async(double workingSet, CancellationToken cancellationToken)
        {
            return await _connection.PublishAsync(
                $"devices/{_connection.ConnectionSettings.DeviceId}/messages/events/",
                new { workingSet }, cancellationToken);
        }
    }
}
