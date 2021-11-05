using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : IHubMqttClient
    {
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        public Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        public Func<PropertyReceived, Task<PropertyAck>> OnPropertyChange { get; set; }
        public Action<MqttApplicationMessageReceivedEventArgs> OnMessage { get; set; }

        const int twinOperationTimeoutSeconds = 5;

        static Action<string> twin_cb;
        static Action<int> patch_cb;
        int lastRid = 1;
        readonly HubMqttConnection connection;
        public ConnectionSettings ConnectionSettings => connection.ConnectionSettings;
        public bool IsConnected => connection.IsConnected;

        public static async Task<IHubMqttClient> CreateAsync(string cs) =>
            await CreateAsync(ConnectionSettings.FromConnectionString(cs));

        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs)
        {
            var mqttConnection = await HubMqttConnection.CreateFromDCSAsync(cs);
            var instance = new HubMqttClient(mqttConnection);
            instance.ConfigureReservedTopics();
            mqttConnection.OnMqttClientDisconnected += (o, e) => instance.OnMqttClientDisconnected?.Invoke(o, e);
            return instance;
        }

        private HubMqttClient(HubMqttConnection conn)
        {
            connection = conn;
        }

        public async Task CloseAsync()
        {
            await connection.CloseAsync();
        }

        public async Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "")
        {
            string topic = $"devices/{connection.ConnectionSettings.DeviceId}";

            if (!string.IsNullOrEmpty(connection.ConnectionSettings.ModuleId))
            {
                topic += $"/modules/{connection.ConnectionSettings.ModuleId}";
            }
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(dtdlComponentname))
            {
                topic += $"$.sub={dtdlComponentname}";
            }
            var pubAck = await connection.PublishAsync(topic, payload);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await connection.PublishAsync($"$iothub/methods/res/{status}/?$rid={rid}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                twin_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                twin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin GET: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                patch_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                patch_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        void ConfigureReservedTopics()
        {
            Trace.TraceWarning("### CONNECTED WITH SERVER ###");
            var subres = connection.MqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                    .WithTopicFilter("$iothub/methods/POST/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                    .WithTopicFilter("$iothub/twin/res/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                    .WithTopicFilter("$iothub/twin/PATCH/properties/desired/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                    .Build()).Result;
            subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

            if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
            {
                throw new ApplicationException("Error subscribing to system topics");
            }


            connection.MqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                string msg = string.Empty;

                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (e.ApplicationMessage.Topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["$rid"]);
                    twinVersion = Convert.ToInt32(qs["$version"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }

                //Trace.TraceWarning($"<- {e.ApplicationMessage.Topic}  {e.ApplicationMessage.Payload?.Length} Bytes");
                if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/200"))
                {
                    twin_cb(msg);
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/204"))
                {
                    patch_cb(twinVersion);
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
                {
                    var ack = await OnPropertyChange?.Invoke(new PropertyReceived()
                    {
                        Rid = rid.ToString(),
                        Topic = e.ApplicationMessage.Topic,
                        PropertyMessageJson = msg,
                        Version = twinVersion
                    });
                    await UpdateTwinAsync(ack.BuildAck());
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/"))
                {
                    var cmdName = segments[3];
                    var resp = await OnCommand?.Invoke(new CommandRequest()
                    {
                        CommandName = cmdName,
                        CommandPayload = msg
                    });
                    await CommandResponseAsync(rid.ToString(), cmdName, resp.Status.ToString(), resp.CommandResponsePayload);
                }
            });
        }
    }
}
