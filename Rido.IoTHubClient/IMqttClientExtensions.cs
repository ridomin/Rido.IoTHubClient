using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Diagnostics;
using MQTTnet.Diagnostics.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public static class IMqttClientExtensions
    {
        public static async Task<MqttClientConnectResult> ConnectWithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string sasKey, string modelId = "", int minutes = 60)
        {
            (string username, string password) = SasAuth.GenerateHubSasCredentials(hostName, deviceId, sasKey, modelId, minutes);
            return await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                 .WithClientId(deviceId)
                 .WithTcpServer(hostName, 8883)
                 .WithCredentials(username, password)
                 .WithCommunicationTimeout(System.TimeSpan.FromSeconds(2))
                 .WithTls(new MqttClientOptionsBuilderTlsParameters
                 {
                     UseTls = true,
                     SslProtocol = SslProtocols.Tls12
                 })
                 .Build());
        }

        public static async Task<MqttClientConnectResult> ConnectWithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string moduleId, string sasKey, string modelId = "", int minutes = 60) =>
            await ConnectWithSasAsync(mqttClient, hostName, $"{deviceId}/{moduleId}", sasKey, modelId, minutes);

        public static async Task<MqttClientConnectResult> ConnectWithX509Async(this IMqttClient mqttClient, string hostName, X509Certificate cert, string modelId = "")
        {
            return await mqttClient.ConnectAsync(
               new MqttClientOptionsBuilder()
                   .WithClientId(cert.Subject[3..])
                   .WithTcpServer(hostName, 8883)
                   .WithCommunicationTimeout(System.TimeSpan.FromSeconds(2))
                   .WithCredentials(new MqttClientCredentials()
                   {
                       Username = SasAuth.GetUserName(hostName, cert.Subject[3..], modelId)
                   })
                   .WithTls(new MqttClientOptionsBuilderTlsParameters
                   {
                       UseTls = true,
                       SslProtocol = SslProtocols.Tls12,
                       Certificates = new List<X509Certificate> { cert }
                   })
                   .Build(),
               CancellationToken.None);
        }

        public static IMqttClient CreateMqttClientWithLogger(TextWriter writer = null)
        {
            Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
            IMqttClient client;
            if (writer == null)
            {
                client = new MqttFactory().CreateMqttClient();
            }
            else
            {
                Trace.Listeners.Add(new TextWriterTraceListener(writer));
                Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);

                MqttNetEventLogger logger = new MqttNetEventLogger();
                logger.LogMessagePublished += (s, e) =>
                {
                    var trace = $">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}]: {e.LogMessage.Message}";
                    if (e.LogMessage.Exception != null)
                    {
                        trace += Environment.NewLine + e.LogMessage.Exception.ToString();
                    }

                    Trace.TraceInformation(trace);
                };
                client = new MqttFactory(logger).CreateMqttClient();
            }
            return client;
        }
    }
}
