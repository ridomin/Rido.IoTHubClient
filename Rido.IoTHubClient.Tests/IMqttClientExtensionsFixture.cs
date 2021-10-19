using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class IMqttClientExtensionsFixture : IDisposable
    {
        const string hostname = "tests.azure-devices.net";
        const string deviceId = "d5";
        static string DefaultKey => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N")));

        readonly IMqttClient mqttClient;
        public IMqttClientExtensionsFixture()
        {
            mqttClient = new MqttFactory().CreateMqttClient();
        }

        [Fact]
        public async Task ConnectWithSaSV1()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, DefaultKey);
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectWithCertsV1()
        {
            var connack = await mqttClient.ConnectWithX509Async(hostname, new X509Certificate("testdevice.pfx", "1234"));
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }
        public void Dispose()
        {
            _ = mqttClient.DisconnectAsync();
            GC.SuppressFinalize(this);
        }
    }
}
