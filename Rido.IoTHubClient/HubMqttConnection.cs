using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class HubMqttConnection : IMqttConnection, IDisposable
    {
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        public bool IsConnected => mqttClient.IsConnected;

        public ConnectionSettings ConnectionSettings { get; internal set; }

        public Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessage { get; set; }

        IMqttClient mqttClient { get; set; }
        static bool reconnecting = false;
        bool disposedValue;
        static Timer timerTokenRenew;
        readonly HashSet<string> subscribedTopics = new HashSet<string>();

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

            mqttClient = new MqttFactory(logger).CreateMqttClient();
            mqttClient.UseApplicationMessageReceivedHandler(m => OnMessage?.Invoke(m));
            mqttClient.UseDisconnectedHandler(async e =>
            {
                Trace.TraceError($"## DISCONNECT ## {e.ClientWasConnected} {e.Reason}");
                OnMqttClientDisconnected?.Invoke(this,
                    new DisconnectEventArgs()
                    {
                        Exception = e.Exception,
                        DisconnectReason = (DisconnectReason)e.Reason,
                        //ResultCode = (ConnResultCode)e.AuthenticateResult?.ResultCode
                    });

                if (ConnectionSettings.RetryInterval > 0 && !reconnecting)
                {
                    try
                    {
                        Trace.TraceWarning($"*** Reconnecting in {ConnectionSettings.RetryInterval} s.. ");
                        await Task.Delay(ConnectionSettings.RetryInterval * 1000);
                        await mqttClient.ReconnectAsync();
                        await SubscribeAsync(subscribedTopics.ToArray<string>());
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

        public static async Task<IMqttConnection> CreateAsync(ConnectionSettings dcs) => await CreateAsync(dcs, CancellationToken.None);
        public static async Task<IMqttConnection> CreateAsync(ConnectionSettings dcs, CancellationToken cancellationToken)
        {
            await ProvisionIfNeeded(dcs);
            var connection = new HubMqttConnection(dcs);
            await connection.ConnectAsync(cancellationToken);
            return connection;
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var dcs = ConnectionSettings;
            MqttClientConnectResult connAck = null;
            if (dcs.Auth == "X509")
            {
                connAck = await ConnectWithCertAsync(cancellationToken);
            }

            if (dcs.Auth == "SAS")
            {
                connAck = await ConnectWithSasAsync(cancellationToken);
            }

            if (connAck?.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new ApplicationException($"Error connecting: {connAck.ResultCode} {connAck.ReasonString}");
            }
        }

        private async Task<MqttClientConnectResult> ConnectWithSasAsync(CancellationToken cancellationToken)
        {
            var dcs = ConnectionSettings;
            MqttClientConnectResult connAck;
            if (string.IsNullOrEmpty(dcs.ModuleId))
            {
                connAck = await mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, cancellationToken, dcs.ModelId, dcs.SasMinutes);
            }
            else
            {
                connAck = await mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.ModuleId, dcs.SharedAccessKey, cancellationToken, dcs.ModelId, dcs.SasMinutes);
            }

            if (connAck?.ResultCode == MqttClientConnectResultCode.Success)
            {
                timerTokenRenew = new Timer(ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
            }
            return connAck;
        }

        private async Task<MqttClientConnectResult> ConnectWithCertAsync(CancellationToken cancellationToken)
        {
            var dcs = ConnectionSettings;
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
            return await mqttClient.ConnectWithX509Async(dcs.HostName, cert, cancellationToken, dcs.ModelId);
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
            if (mqttClient.IsConnected)
            {
                Trace.TraceWarning("Forced Diconnection");
                await mqttClient.DisconnectAsync();
            }
        }

        void ReconnectWithToken(object state)
        {
            Trace.TraceWarning("*** REFRESHING TOKEN *** ");
            reconnecting = true;
            timerTokenRenew.Dispose();
            var dcs = ConnectionSettings;

            CloseAsync().Wait();
            ConnectAsync(CancellationToken.None).Wait();
            _ = SubscribeAsync(subscribedTopics.ToArray<string>());

            Trace.TraceWarning($"Refreshed Result: {mqttClient.IsConnected}");
            reconnecting = false;
            timerTokenRenew = new Timer(ReconnectWithToken, null, (dcs.SasMinutes - 1) * 60 * 1000, 0);
        }

        public async Task<MqttClientSubscribeResult> SubscribeAsync(string topic, CancellationToken cancellationToken = default) => await SubscribeAsync(new string[] { topic }, cancellationToken);

        public async Task<MqttClientSubscribeResult> SubscribeAsync(string[] topics, CancellationToken cancellationToken = default)
        {
            var subBuilder = new MqttClientSubscribeOptionsBuilder();
            foreach (string topic in topics.Except(subscribedTopics))
            {
                subBuilder.WithTopicFilter(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                subscribedTopics.Add(topic);
            }

            if (subBuilder.Build().TopicFilters.Count > 0)
            {
                return await mqttClient.SubscribeAsync(subBuilder.Build(), cancellationToken);
            }
            else
            {
                return new MqttClientSubscribeResult();
            }
            //topics.ToList().ForEach(t => subBuilder.WithTopicFilter(t, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce));
        }

        public async Task<MqttClientPublishResult> PublishAsync(string topic, object payload) => await PublishAsync(topic, payload, CancellationToken.None);
        public async Task<MqttClientPublishResult> PublishAsync(string topic, object payload, CancellationToken cancellationToken)
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
            if (IsConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    pubRes = await mqttClient.PublishAsync(message, cancellationToken);
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
                    mqttClient.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}
