using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
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
        public async Task ConnectDeviceWithSaS()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, DefaultKey);
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectDeviceWithSaSAndModelId()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, DefaultKey, "dtmi:rido:test;1", 5);
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectModuleWithSaS()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, "m1", DefaultKey, String.Empty);
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectModuleWithSaSAndModelId()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, "m1", DefaultKey, "dtmi:rido:module;1");
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectDeviceWithCert()
        {
            var connack = await mqttClient.ConnectWithX509Async(hostname, new X509Certificate("testdevice.pfx", "1234"));
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task SendTelemetryWithHeaders()
        {
            var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, DefaultKey, "dmit:com:example:Thermostat;1", 60);
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
            MqttApplicationMessage msg = new();
            msg.Payload = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { temperature = 432 }));
            msg.Topic = $"devices/{deviceId}/messages/events/$.sub=mycomp";
            var puback = await mqttClient.PublishAsync(msg);
            Console.WriteLine(puback.ReasonCode);
        }

        [Fact]
        public async Task ConnectDeviceWithCertAndModelID()
        {
            var connack = await mqttClient.ConnectWithX509Async(hostname, new X509Certificate("testdevice.pfx", "1234"), "dtmi:rido:device;1");
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectModuleWithCert()
        {
            var connack = await mqttClient.ConnectWithX509Async(hostname, new X509Certificate("xd01_xmod01.pfx", "1234"));
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        [Fact]
        public async Task ConnectModuleWithCertAndModelId()
        {
            var connack = await mqttClient.ConnectWithX509Async(hostname, new X509Certificate("xd01_xmod01.pfx", "1234"), "dtmi:rido:module;1");
            Assert.Equal(MqttClientConnectResultCode.Success, connack.ResultCode);
        }

        public void Dispose()
        {
            _ = mqttClient.DisconnectAsync();
            GC.SuppressFinalize(this);
        }
    }
}
