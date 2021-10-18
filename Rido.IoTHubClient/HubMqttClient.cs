using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : IHubMqttClient
    {
        const int refreshTokenInterval = 3540000; //59 mins

        public bool IsConnected => mqttClient.IsConnected;
        public event EventHandler<CommandEventArgs> OnCommandReceived;
        public event EventHandler<PropertyEventArgs> OnPropertyReceived;
        public DeviceConnectionString DeviceConnectionString { get; private set; }
        public string CertInfo;

        IMqttClient mqttClient;
        static Timer timerTokenRenew;

        static Action<string> twin_cb;
        static Action<int> patch_cb;
        int lastRid = 1;
        bool reconnecting = false;

        private HubMqttClient()
        {
            MqttNetLogger logger = new MqttNetLogger();
            logger.LogMessagePublished += (s, e) =>
            {
                var trace = $">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}]: {e.LogMessage.Message}";
                if (e.LogMessage.Exception != null)
                {
                    trace += Environment.NewLine + e.LogMessage.Exception.ToString();
                }

                Trace.TraceInformation(trace);
            };
            mqttClient = new MqttFactory(logger).CreateMqttClient();
            ConfigureReservedTopics();
        }

        public static async Task<HubMqttClient> CreateFromConnectionStringAsync(string connectionString) =>
            await CreateFromDCSAsync(new DeviceConnectionString(connectionString));

        public static async Task<HubMqttClient> CreateAsync(string hostName, string deviceId, string sasKey, string modelId = "") =>
            await CreateFromDCSAsync(new DeviceConnectionString() { DeviceId = deviceId, HostName = hostName, SharedAccessKey = sasKey, ModelId = modelId });

        // TODO: Review overloads, easy to conflict with the optional param
        public static async Task<HubMqttClient> CreateAsync(string hostName, string deviceId, string moduleId, string sasKey, string modelId = "") =>
           await CreateFromDCSAsync(new DeviceConnectionString() { HostName = hostName, DeviceId = deviceId, ModuleId = moduleId, SharedAccessKey = sasKey, ModelId = modelId });

        private static async Task<HubMqttClient> CreateFromDCSAsync(DeviceConnectionString dcs)
        {
            var client = new HubMqttClient();
            MqttClientAuthenticateResult connAck;
            if (string.IsNullOrEmpty(dcs.ModuleId))
            {
                connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId, 60);
            }
            else
            {
                connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.ModuleId, dcs.SharedAccessKey, dcs.ModelId, 60);
            }

            if (connAck.ResultCode == MqttClientConnectResultCode.Success)
            {
                timerTokenRenew = new Timer(client.ReconnectWithToken, null, refreshTokenInterval, 0);
            }
            else
            {
                throw new ApplicationException($"Error connecting: {connAck.ResultCode} {connAck.ReasonString}");
            }

            client.DeviceConnectionString = dcs;
            return client;
        }

        public static async Task<HubMqttClient> CreateWithClientCertsAsync(string hostname, string certPath, string certPwd, string modelId = "")
        {
            using var cert = new X509Certificate2(certPath, certPwd);
            string certInfo = $"{cert.SubjectName.Name} issued by {cert.IssuerName.Name} NotAfter {cert.GetExpirationDateString()} ({cert.Thumbprint})";
            Trace.TraceInformation(certInfo);
            var cid = cert.Subject.Substring(3);
            string deviceId = cid;
            string moduleId = string.Empty;

            if (cid.Contains("/")) // is a module
            {
                var segments = cid.Split('/');
                deviceId = segments[0];
                moduleId = segments[1];
            }

            var client = new HubMqttClient();
            var connack = await client.mqttClient.ConnectWithX509Async(hostname, cert, modelId);
            if (connack.ResultCode == MqttClientConnectResultCode.Success)
            {
                client.DeviceConnectionString = new DeviceConnectionString($"HostName={hostname};DeviceId={deviceId};ModuleId={moduleId};Auth=X509");
                client.CertInfo = certInfo;
            }
            else
            {
                throw new ApplicationException($"Error connecting: {connack.ResultCode} {connack.ReasonString}");
            }

            return client;
        }

        public async Task<MqttClientPublishResult> SendTelemetryAsync(object payload) =>
            await PublishAsync($"devices/{this.DeviceConnectionString.DeviceId}/messages/events/", payload);

        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await PublishAsync($"$iothub/methods/res/{status}/?$rid={rid}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            var puback = await PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                twin_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                twin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin GET: {s}"));
            }
            return tcs.Task.Result;
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>();
            var puback = await PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                patch_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                patch_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
            }
            return tcs.Task.Result;
        }

        public async Task CloseAsync()
        {
            var unsuback = await mqttClient.UnsubscribeAsync(new string[]
            {
                "$iothub/methods/POST/#",
                "$iothub/twin/res/#",
                "$iothub/twin/PATCH/properties/desired/#"
            });
            unsuback.Items.ToList().ForEach(i => Trace.TraceInformation($"- {i.TopicFilter} {i.ReasonCode}"));
            await mqttClient.DisconnectAsync();
        }

        async Task<MqttClientPublishResult> PublishAsync(string topic, object payload)
        {
            while (!mqttClient.IsConnected && reconnecting)
            {
                Trace.TraceWarning(" !! waiting 100 ms to publish ");
                await Task.Delay(100);
            }

            string jsonPayload;
            if (payload is string)
            {
                jsonPayload = payload as string;
            }
            else
            {
                jsonPayload = JsonSerializer.Serialize(payload);
            }
            var message = new MqttApplicationMessageBuilder()
                              .WithTopic(topic)
                              .WithPayload(jsonPayload)
                              .Build();

            MqttClientPublishResult pubRes;
            if (mqttClient.IsConnected)
            {
                try
                {
                    pubRes = await mqttClient.PublishAsync(message, CancellationToken.None);
                    if (pubRes.ReasonCode != MqttClientPublishReasonCode.Success)
                    {
                        Trace.TraceError(pubRes.ReasonCode + pubRes.ReasonString);
                    }
                    //Trace.TraceInformation($"-> {topic} {message.Payload?.Length} Bytes {pubRes.ReasonCode}");
                    return pubRes;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Connected?: {mqttClient.IsConnected} Reconnecting?:{reconnecting}");
                    Trace.TraceError(" !!!!!  Failed one message " + ex);
                    return null;
                }
            }
            else
            {
                Trace.TraceWarning(" !!!!!  Missing one message ");
                // TODO: Reconnect here?
                return null;
            }
        }

        void ConfigureReservedTopics()
        {
            mqttClient.UseConnectedHandler(async e =>
            {
                Trace.TraceWarning("### CONNECTED WITH SERVER ###");
                var subres = await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                        .WithTopicFilter("$iothub/methods/POST/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .WithTopicFilter("$iothub/twin/res/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .WithTopicFilter("$iothub/twin/PATCH/properties/desired/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .Build());
                subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            });

            mqttClient.UseDisconnectedHandler(e =>
            {
                Trace.TraceError("## DISCONNECT ##");
                Trace.TraceError($"** {e.ClientWasConnected} {e.Reason}");
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string msg = string.Empty;

                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (e.ApplicationMessage.Topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[segments.Length - 1]);
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
                    OnPropertyReceived?.Invoke(this, new PropertyEventArgs()
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Rid = rid.ToString(),
                        PropertyMessageJson = TwinProperties.RemoveVersion(msg),
                        Version = twinVersion
                    });
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/"))
                {
                    var cmdName = segments[3];
                    //Trace.TraceWarning($"<- {e.ApplicationMessage.Topic} {cmdName} {e.ApplicationMessage.Payload.Length} Bytes");
                    OnCommandReceived?.Invoke(this, new CommandEventArgs()
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Rid = rid.ToString(),
                        CommandName = cmdName,
                        CommandRequestMessageJson = msg
                    });
                }
            });
        }

        void ReconnectWithToken(object state)
        {
            lock (this)
            {
                reconnecting = true;
                Trace.TraceWarning("*** REFRESHING TOKEN *** ");
                timerTokenRenew.Dispose();
                CloseAsync().Wait();
                var dcs = DeviceConnectionString;
                mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId, 60).Wait();
                reconnecting = false;
                timerTokenRenew = new Timer(ReconnectWithToken, null, refreshTokenInterval, 0);
            }
        }
    }
}
