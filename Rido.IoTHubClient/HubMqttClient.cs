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
    public class HubMqttClient : IDisposable
    //: IHubMqttClient, IDisposable
    {
        public bool IsConnected => mqttClient.IsConnected;

        public Func<CommandRequest, CommandResponse> OnCommand;
        public Func<PropertyReceived, PropertyAck> OnPropertyChange;

        //public event EventHandler<PropertyEventArgs> OnPropertyReceived;
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;    

        public ConnectionSettings ConnectionSettings { get; private set; }

        const int twinOperationTimeoutSeconds = 5;
        IMqttClient mqttClient;
        static Timer timerTokenRenew;

        static Action<string> twin_cb;
        static Action<int> patch_cb;
        int lastRid = 1;
        bool reconnecting = false;
        private bool disposedValue;

        private HubMqttClient(ConnectionSettings cs)
        {
            ConnectionSettings = cs;
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
                OnMqttClientDisconnected?.Invoke(this,
                    new DisconnectEventArgs()
                    {
                        Exception = e.Exception,
                        DisconnectReason = (DisconnectReason)e.Reason,
                        //ResultCode = (ConnResultCode)e.AuthenticateResult?.ResultCode
                    });

                if (ConnectionSettings.RetryInterval > 0)
                {
                    try
                    {
                        Trace.TraceWarning($"*** Reconnecting in {ConnectionSettings.RetryInterval} s.. ");
                        await Task.Delay(ConnectionSettings.RetryInterval * 1000);
                        await mqttClient.ReconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                    }
                }
                else
                {
                    Trace.TraceWarning($"*** Reconnecting Disabled {ConnectionSettings.RetryInterval}");
                }
            });
        }

        public static async Task<HubMqttClient> CreateFromConnectionStringAsync(string connectionString) =>
            await CreateFromDCSAsync(ConnectionSettings.FromConnectionString(connectionString));

        public static async Task<HubMqttClient> CreateAsync(string hostName, string deviceId, string sasKey, string modelId = "") =>
            await CreateFromDCSAsync(new ConnectionSettings() { DeviceId = deviceId, HostName = hostName, SharedAccessKey = sasKey, ModelId = modelId });

        // TODO: Review overloads, easy to conflict with the optional param
        public static async Task<HubMqttClient> CreateAsync(string hostName, string deviceId, string moduleId, string sasKey, string modelId = "") =>
           await CreateFromDCSAsync(new ConnectionSettings() { HostName = hostName, DeviceId = deviceId, ModuleId = moduleId, SharedAccessKey = sasKey, ModelId = modelId });

        public static async Task<HubMqttClient> CreateFromDCSAsync(ConnectionSettings dcs)
        {
            await ProvisionIfNeeded(dcs);

            var client = new HubMqttClient(dcs);
            MqttClientAuthenticateResult connAck = null;

            if (dcs.Auth == "X509")
            {
                var segments = dcs.X509Key.Split('|');
                string pfxpath = segments[0];
                string pfxpwd = segments[1];
                X509Certificate2 cert = new X509Certificate2(pfxpath, pfxpwd);
                var cid = cert.Subject[3..];
                string deviceId = cid;
                string moduleId = string.Empty;

                if (cid.Contains("/")) // is a module
                {
                    var segmentsId = cid.Split('/');
                    dcs.DeviceId = segmentsId[0];
                    dcs.ModuleId = segmentsId[1];

                }
                connAck = await client.mqttClient.ConnectWithX509Async(dcs.HostName, cert, dcs.ModelId);
                if (connAck.ResultCode == MqttClientConnectResultCode.Success)
                {
                    client.ConnectionSettings = dcs;
                }
            }

            if (dcs.Auth == "SAS")
            {
                if (string.IsNullOrEmpty(dcs.ModuleId))
                {
                    connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
                }
                else
                {
                    connAck = await client.mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.ModuleId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
                }

                if (connAck?.ResultCode == MqttClientConnectResultCode.Success)
                {

                    client.ConnectionSettings = dcs;
                    timerTokenRenew = new Timer(client.ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
                }
                else
                {
                    throw new ApplicationException($"Error connecting: {connAck.ResultCode} {connAck.ReasonString}");
                }
            }

            return client;
        }

        private static async Task ProvisionIfNeeded(ConnectionSettings dcs)
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

            var client = new HubMqttClient(ConnectionSettings.FromConnectionString($"HostName={hostname};DeviceId={deviceId};ModuleId={moduleId};Auth=X509"));
            var connack = await client.mqttClient.ConnectWithX509Async(hostname, cert, modelId);
            if (connack.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new ApplicationException($"Error connecting: {connack.ResultCode} {connack.ReasonString}");
            }

            return client;
        }

        public async Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "")
        {
            string topic = $"devices/{ConnectionSettings.DeviceId}";

            if (!string.IsNullOrEmpty(ConnectionSettings.ModuleId))
            {
                topic += $"/modules/{ConnectionSettings.ModuleId}";
            }
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(dtdlComponentname))
            {
                topic += $"$.sub={dtdlComponentname}";
            }
            var pubAck = await PublishAsync(topic, payload);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

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
            return tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds)).Result;
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
            return tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds)).Result;
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

                if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
                {
                    throw new ApplicationException("Error subscribing to system topics");
                }
            });



            mqttClient.UseApplicationMessageReceivedHandler(async e =>
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
                    var ack = OnPropertyChange?.Invoke(new PropertyReceived()
                    {
                        Rid = rid.ToString(),
                        Topic = e.ApplicationMessage.Topic,
                        PropertyMessageJson = msg,
                        Version = twinVersion
                    });
                    await UpdateTwinAsync(ack.BuildAck()); 

                    //OnPropertyReceived?.Invoke(this, new PropertyEventArgs()
                    //{
                    //    Topic = e.ApplicationMessage.Topic,
                    //    Rid = rid.ToString(),
                    //    PropertyMessageJson = TwinProperties.RemoveVersion(msg),
                    //    Version = twinVersion
                    //});
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/"))
                {
                    var cmdName = segments[3];
                    var resp = OnCommand?.Invoke(new CommandRequest()
                    {
                        _rid = rid.ToString(),
                        CommandName = cmdName,
                        CommandPayload = msg
                    });
                    await CommandResponseAsync(resp._rid, cmdName, resp._status.ToString(), resp.CommandResponsePayload);
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
                var dcs = ConnectionSettings;
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
