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
        public static async Task<MqttClientAuthenticateResult> ConnectWithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string sasKey, string modelId = "", int minutes = 60)
        {
            (string username, byte[] password) = SasAuth.GenerateHubSasCredentials(hostName, deviceId, sasKey, modelId, minutes);
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

        //public static async Task<MqttClientAuthenticateResult> ConnectWithSasAsync(this IMqttClient mqttClient, string hostName, string deviceId, string moduleId, string sasKey, string modelId = "", int minutes = 60)
        //{
        //    (string username, byte[] password) = SasAuth.GenerateHubSasCredentials(hostName, deviceId, moduleId, sasKey, modelId, minutes);
        //    return await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
        //         .WithClientId($"{deviceId}/{moduleId}")
        //         .WithTcpServer(hostName, 8883)
        //         .WithCredentials(username, password)
        //         .WithTls(new MqttClientOptionsBuilderTlsParameters
        //         {
        //             UseTls = true,
        //             SslProtocol = SslProtocols.Tls12
        //         })
        //         .Build());
        //}

        public static async Task<MqttClientAuthenticateResult> ConnectWithX509Async(this IMqttClient mqttClient, string hostName, X509Certificate cert, string modelId = "")
        {
            var cid = cert.Subject[3..];
            string deviceId = cid;
            string moduleId = string.Empty;
            
            if (cid.Contains("/")) // is a module
            {
                var segments = cid.Split('/');
                deviceId = segments[0];
                moduleId = segments[1];
            }
                                 
            string username = SasAuth.GetUserName(hostName, deviceId, moduleId, string.Empty, modelId, AuthType.X509);
            
            return await mqttClient.ConnectAsync(
               new MqttClientOptionsBuilder()
                   .WithClientId(cid)
                   .WithTcpServer(hostName, 8883)
                   .WithCredentials(new MqttClientCredentials()
                   {
                       Username = username
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
