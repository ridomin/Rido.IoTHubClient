using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Publishing;
using MQTTnet.Diagnostics.Logger;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class HubMqttConnection : IHubMqttConnection, IDisposable
    {
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        internal IMqttClient MqttClient { get; private set; }
        public bool IsConnected => MqttClient.IsConnected;

        public ConnectionSettings ConnectionSettings { get; internal set; }

        bool reconnecting = false;
        bool disposedValue;
        static Timer timerTokenRenew;

        private HubMqttConnection(ConnectionSettings cs)
        {
            ConnectionSettings = cs;
            var logger = new MqttNetEventLogger();
            logger.LogMessagePublished += (s, e) =>
            {
                var trace = $">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}]: {e.LogMessage.Message}";
                if (e.LogMessage.Exception != null)
                {
                    trace += Environment.NewLine + e.LogMessage.Exception.ToString();
                }

                Trace.TraceInformation(trace);
            };

            MqttClient = new MqttFactory(logger).CreateMqttClient();
            MqttClient.UseDisconnectedHandler(async e =>
            {
                Trace.TraceError($"## DISCONNECT ## {e.ClientWasConnected} {e.Reason}");
                OnMqttClientDisconnected?.Invoke(this,
                    new DisconnectEventArgs()
                    {
                        Exception = e.Exception,
                        DisconnectReason = (DisconnectReason)e.Reason,
                        //ResultCode = (ConnResultCode)e.AuthenticateResult?.ResultCode
                    });

                if (ConnectionSettings.RetryInterval > 0 &&  !reconnecting)
                {
                    try
                    {
                        Trace.TraceWarning($"*** Reconnecting in {ConnectionSettings.RetryInterval} s.. ");
                        await Task.Delay(ConnectionSettings.RetryInterval * 1000);
                        await MqttClient.ReconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                    }
                }
                else
                {
                    Trace.TraceWarning($"*** Skipping Reconnect. reconnecting={reconnecting}  RetryInterval={ConnectionSettings.RetryInterval}");
                }
            });
        }

        public static async Task<HubMqttConnection> CreateFromDCSAsync(ConnectionSettings dcs)
        {
            await ProvisionIfNeeded(dcs);
            var client = new HubMqttConnection(dcs);

            MqttClientConnectResult connAck = null;

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
                connAck = await client.MqttClient.ConnectWithX509Async(dcs.HostName, cert, dcs.ModelId);
                if (connAck.ResultCode != MqttClientConnectResultCode.Success)
                {
                    Trace.TraceError("ERROR CONNECTING {0} {1} {2}", connAck.ResultCode, connAck.ReasonString, connAck.AuthenticationMethod);
                    throw new SecurityException("Authentication failed: " + connAck.ReasonString);
                }
            }

            if (dcs.Auth == "SAS")
            {
                if (string.IsNullOrEmpty(dcs.ModuleId))
                {
                    connAck = await client.MqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
                }
                else
                {
                    connAck = await client.MqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.ModuleId, dcs.SharedAccessKey, dcs.ModelId, dcs.SasMinutes);
                }

                if (connAck?.ResultCode == MqttClientConnectResultCode.Success)
                {
                    timerTokenRenew = new Timer(client.ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
                }
                else
                {
                    throw new ApplicationException($"Error connecting: {connAck.ResultCode} {connAck.ReasonString}");
                }
            }

            return client;
        }

        public static async Task<HubMqttConnection> CreateWithClientCertsAsync(string hostname, X509Certificate2 cert, string modelId = "")
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

            var client = new HubMqttConnection(ConnectionSettings.FromConnectionString($"HostName={hostname};DeviceId={deviceId};ModuleId={moduleId};Auth=X509"));
            var connack = await client.MqttClient.ConnectWithX509Async(hostname, cert, modelId);
            if (connack.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new ApplicationException($"Error connecting: {connack.ResultCode} {connack.ReasonString}");
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
        public async Task CloseAsync()
        {
            if (MqttClient.IsConnected)
            {
                var unsuback = await MqttClient.UnsubscribeAsync(new string[]
                {
                    "$iothub/methods/POST/#",
                    "$iothub/twin/res/#",
                    "$iothub/twin/PATCH/properties/desired/#"
                });
                unsuback.Items.ToList().ForEach(i => Trace.TraceInformation($"- {i.TopicFilter} {i.ReasonCode}"));
                await MqttClient.DisconnectAsync();
            }
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
                MqttClient = CreateFromDCSAsync(dcs).Result.MqttClient;
                Trace.TraceWarning($"Refreshed Result: {MqttClient.IsConnected}");
                reconnecting = false;
                timerTokenRenew = new Timer(ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
            }
        }

        public async Task<MqttClientPublishResult> PublishAsync(string topic, object payload)
        {
            while (!IsConnected && reconnecting)
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
            if (IsConnected)
            {
                try
                {
                    pubRes = await MqttClient.PublishAsync(message, CancellationToken.None);
                    if (pubRes.ReasonCode != MqttClientPublishReasonCode.Success)
                    {
                        Trace.TraceError(pubRes.ReasonCode + pubRes.ReasonString);
                    }
                    //Trace.TraceInformation($"-> {topic} {message.Payload?.Length} Bytes {pubRes.ReasonCode}");
                    return pubRes;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Connected?: {IsConnected} Reconnecting?:{reconnecting}");
                    Trace.TraceError(" !!!!!  Failed one message " + ex);
                    return new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError };
                }
            }
            else
            {
                Trace.TraceWarning(" !!!!!  Missing one message ");
                return new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError };
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MqttClient.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}
