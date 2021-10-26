using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
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
    public class HubMqttClient : IHubMqttClient, IDisposable
    {
        public bool IsConnected => mqttClient.IsConnected;
        public event EventHandler<CommandEventArgs> OnCommandReceived;
        public event EventHandler<PropertyEventArgs> OnPropertyReceived;
        public event EventHandler<MqttClientDisconnectedEventArgs> OnMqttClientDisconnected;

        public DeviceConnectionString DeviceConnectionString { get; private set; }

        IMqttClient mqttClient;
        static Timer timerTokenRenew;

        static Action<string> twin_cb;
        static Action<int> patch_cb;
        int lastRid = 1;
        bool reconnecting = false;
        private bool disposedValue;

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
            mqttClient.UseDisconnectedHandler(async e =>
            {
                Trace.TraceError("## DISCONNECT ##");
                Trace.TraceError($"** {e.ClientWasConnected} {e.Reason}");
                OnMqttClientDisconnected?.Invoke(this, e);

                if (DeviceConnectionString.RetryInterval > 0)
                {
                    try
                    {
                        Trace.TraceWarning($"*** Reconnecting in {DeviceConnectionString.RetryInterval} s.. ");
                        await Task.Delay(DeviceConnectionString.RetryInterval * 1000);
                        await mqttClient.ReconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                    }
                }
                else
                {
                    Trace.TraceWarning($"*** Reconnecting Disabled {DeviceConnectionString.RetryInterval}");
                }
            });
        }

        public static async Task<IHubMqttClient> CreateFromConnectionStringAsync(string connectionString) =>
            await CreateFromDCSAsync(new DeviceConnectionString(connectionString));

        public static async Task<IHubMqttClient> CreateAsync(string hostName, string deviceId, string sasKey, string modelId = "") =>
            await CreateFromDCSAsync(new DeviceConnectionString() { DeviceId = deviceId, HostName = hostName, SharedAccessKey = sasKey, ModelId = modelId });

        // TODO: Review overloads, easy to conflict with the optional param
        public static async Task<IHubMqttClient> CreateAsync(string hostName, string deviceId, string moduleId, string sasKey, string modelId = "") =>
           await CreateFromDCSAsync(new DeviceConnectionString() { HostName = hostName, DeviceId = deviceId, ModuleId = moduleId, SharedAccessKey = sasKey, ModelId = modelId });

        public static async Task<HubMqttClient> CreateFromDCSAsync(DeviceConnectionString dcs)
        {
            await ProvisionIfNeeded(dcs);
            Console.WriteLine(dcs);

            var client = new HubMqttClient();
            MqttClientAuthenticateResult connAck;
            connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
            //if (string.IsNullOrEmpty(dcs.ModuleId))
            //{
            //}
            //else
            //{
            //   connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.ModuleId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
            //}

                if (connAck?.ResultCode == MqttClientConnectResultCode.Success)
                {

                    client.DeviceConnectionString = dcs;
                    timerTokenRenew = new Timer(client.ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
                }
                else
                {
                    throw new ApplicationException($"Error connecting: {connAck.ResultCode} {connAck.ReasonString}");
                }
            

            return client;
        }

        private static async Task ProvisionIfNeeded(DeviceConnectionString dcs)
        {
            if (!string.IsNullOrEmpty(dcs.IdScope))
            {
                DpsStatus dpsResult;
                if (!string.IsNullOrEmpty(dcs.SharedAccessKey))
                {
                    dpsResult = await DpsClient.ProvisionWithSasAsync(dcs.IdScope, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId);
                }
                else if (!string.IsNullOrEmpty(dcs.X509Key))
                {
                    var segments = dcs.X509Key.Split('|');
                    string pfxpath = segments[0];
                    string pfxpwd = segments[1];
                    dpsResult = await DpsClient.ProvisionWithCertAsync(dcs.IdScope, pfxpath, pfxpwd, dcs.ModelId);
                }
                else
                {
                    throw new ApplicationException("No Key found to provision");
                }

                if (!string.IsNullOrEmpty(dpsResult.registrationState.assignedHub))
                {
                    dcs.HostName = dpsResult.registrationState.assignedHub;
                }
                else
                {
                    throw new ApplicationException("DPS Provision failed: " + dpsResult.status);
                }
            }
        }

        public static async Task<HubMqttClient> CreateWithClientCertsAsync(string hostname, X509Certificate2 cert, string modelId = "")
        {
            string certInfo = $"{cert.SubjectName.Name} issued by {cert.IssuerName.Name} NotAfter {cert.GetExpirationDateString()} ({cert.Thumbprint})";
            Trace.TraceInformation(certInfo);
            var cid = cert.Subject[3..];
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
            }
            else
            {
                throw new ApplicationException($"Error connecting: {connack.ResultCode} {connack.ReasonString}");
            }

            return client;
        }

        public async Task<MqttClientPublishResult> SendTelemetryAsync(object payload, string dtdlComponentname = "")
        {
            string topic = $"$az/iot/telemetry";

            //if (!string.IsNullOrEmpty(DeviceConnectionString.ModuleId))
            //{
            //    topic += $"/modules/{DeviceConnectionString.ModuleId}";
            //}
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(dtdlComponentname))
            {
                topic += $"$.sub={dtdlComponentname}";
            }
            return await PublishAsync(topic, payload);
        }

        // TODO: review topic for cmd response
        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await PublishAsync($"$az/iot/methods/res/{status}/?rid={rid}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            var puback = await PublishAsync($"$az/iot/twin/get/?rid={lastRid++}", string.Empty);
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
            var puback = await PublishAsync($"$az/iot/twin/patch/reported/?rid={lastRid++}", payload);
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
                "$az/iot/methods/+/+",
                "$az/iot/twin/get/response/+",
                "$az/iot/twin/patch/response/+",
                "$az/iot/twin/events/desired-changed/+"
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
                return null;
            }
        }

        void ConfigureReservedTopics()
        {
            mqttClient.UseConnectedHandler(async e =>
            {
                Trace.TraceWarning("### CONNECTED WITH SERVER ###");
                var subres = await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                        .WithTopicFilter("$az/iot/methods/+/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/get/response/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/patch/response/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/events/desired-changed/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .Build());
                subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

                if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
                {
                    throw new ApplicationException("Error subscribing to system topics");
                }
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
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["rid"]);
                    twinVersion = Convert.ToInt32(qs["v"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }
    
                //Trace.TraceWarning($"<- {e.ApplicationMessage.Topic}  {e.ApplicationMessage.Payload?.Length} Bytes");
                if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/get/response"))
                {
                    twin_cb(msg);   
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/patch/response/"))
                {
                    patch_cb(twinVersion);
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/events/desired-changed"))
                {
                    OnPropertyReceived?.Invoke(this, new PropertyEventArgs()
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Rid = rid.ToString(),
                        PropertyMessageJson = TwinProperties.RemoveVersion(msg),
                        Version = twinVersion
                    });
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/methods"))
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
                this.mqttClient = CreateFromDCSAsync(dcs).Result.mqttClient;
                reconnecting = false;
                timerTokenRenew = new Timer(ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    mqttClient.Dispose();
                }
                disposedValue = true;
            }
        }


        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
