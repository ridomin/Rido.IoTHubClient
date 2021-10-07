using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public static class IMqttClientExtensions
    {
        public static async Task<MqttClientAuthenticateResult> ConnectWithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string sasKey, int minutes)
        {
            (string username, string password) = SasAuth.GenerateHubSasCredentials(hostName, deviceId, sasKey, minutes);
            return await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                 .WithClientId(deviceId)
                 .WithTcpServer(hostName, 8883)
                 .WithCredentials(username, password)
                 .WithTls(new MqttClientOptionsBuilderTlsParameters
                 {
                     UseTls = true,
                     SslProtocol = SslProtocols.Tls12
                 })
                 .Build());
        }

        public static async Task<MqttClientAuthenticateResult> ConnectV2WithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string sasKey, int minutes)
        {
            (string username, byte[] password) = SasAuthV2.GenerateHubSasCredentials(hostName, deviceId, sasKey, minutes);
            return await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                 .WithClientId(deviceId)
                 .WithTcpServer(hostName, 8883)
                 .WithCredentials(username, password)
                 .WithTls(new MqttClientOptionsBuilderTlsParameters
                 {
                     UseTls = true,
                     SslProtocol = SslProtocols.Tls12
                 })
                 .Build());
        }

        public static async Task<MqttClientAuthenticateResult> ConnectWithX509Async(this IMqttClient mqttClient, string hostName, X509Certificate cert)
        {
            return await mqttClient.ConnectAsync(
               new MqttClientOptionsBuilder()
                   .WithClientId(cert.Subject.Substring(3))
                   .WithTcpServer(hostName, 8883)
                   .WithCredentials(new MqttClientCredentials()
                   {
                       Username = SasAuth.GetUserName(hostName, cert.Subject.Substring(3))
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

        public static async Task<MqttClientAuthenticateResult> ConnectV2WithX509Async(this IMqttClient mqttClient, string hostName, X509Certificate cert)
        {
            return await mqttClient.ConnectAsync(
               new MqttClientOptionsBuilder()
                   .WithClientId(cert.Subject.Substring(3))
                   .WithTcpServer(hostName, 8883)
                   .WithCredentials(new MqttClientCredentials()
                   {
                       Username = SasAuthV2.GetUserName(hostName, cert.Subject.Substring(3))
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
    }
}
