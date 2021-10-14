using System;
using Xunit;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using Azure.Identity;
using Rido.IoTHubClient;
using Xunit.Abstractions;
using System.Text.Json;
namespace Rido.IoTHubClient.Tests
{
    public class HubBrokerMqttClientFixture : IDisposable
    {
        RegistryManager rm;
        string hubName = "broker.azure-devices.net";
        string deviceId = "testsas" + new Random().Next(10);
        
        Device device;

        private readonly ITestOutputHelper output;

        public HubBrokerMqttClientFixture(ITestOutputHelper output)
        {
           // var tokenCredential = new DefaultAzureCredential();
            rm = RegistryManager.CreateFromConnectionString("HostName=broker.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=HbdIWLOaSHdaL5xmF0OhiC0kmDHPinOyI0kISxZ0Rt0=");
            device = GetOrCreateDevice(deviceId).Result;
            this.output = output;
        }

        [Fact]
        public async Task ConnectWithCertKey()
        {
            var xdevice = GetOrCreateDevice("testdevice", true);
            var client = await HubBrokerMqttClient.CreateWithClientCertsAsync(hubName, "testdevice.pfx", "1234");
            Assert.True(client.IsConnected);

            //var t = await client.GetTwinAsync();
            //Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKey()
        {
            var client = await HubBrokerMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact(Skip = "ReservedTopics not ready")]
        public async Task GetTwin()
        {
            var client = await HubBrokerMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var t = await client.GetTwinAsync();
            output.WriteLine(t);
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact(Skip = "ReservedTopics not ready")]
        public async Task UpdateTwin()
        {
            var client = await HubBrokerMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var tick = Environment.TickCount;
            var p = await client.UpdateTwinAsync(new { myProp = tick });

            output.WriteLine("PATCHED:" + p.ToString());

            await Task.Delay(2000);
            var twin = await rm.GetTwinAsync(deviceId);
            Assert.Contains(tick.ToString(), twin.ToJson());
            output.WriteLine(twin.ToJson());
        }

        [Fact(Skip = "ReservedTopics not ready")]
        public async Task ReceiveUpdate()
        {
            var client = await HubBrokerMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool propertyReceived = false;
            client.OnPropertyReceived += async (s, e) =>
            {
                output.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                await Task.Delay(500);

                var ack = TwinProperties.BuildAck(e.PropertyMessageJson, e.Version, 200, "update ok");
                var v = await client.UpdateTwinAsync(ack);
                Console.WriteLine("PATCHED ACK: " + v);
                propertyReceived = true;
            };
            var twin = await rm.GetTwinAsync(deviceId);
            twin.Properties.Desired["myDProp"] = "some value";
            await rm.UpdateTwinAsync(deviceId, twin, twin.ETag);

            await Task.Delay(3000);
            Assert.True(propertyReceived);
            await client.CloseAsync();
        }

        private async Task<Device> GetOrCreateDevice(string deviceId, bool x509 = false)
        {
            var device = await rm.GetDeviceAsync(deviceId);
            if (device == null)
            {
                var d = new Device(deviceId);
                if (x509)
                {
                    d.Authentication = new AuthenticationMechanism()
                    {
                        Type = AuthenticationType.CertificateAuthority
                    };
                }
                device = await rm.AddDeviceAsync(d);
            }

            return device;
        }

        public void Dispose()
        {
            rm.RemoveDeviceAsync(deviceId).Wait();
        }
    }
}
