using Microsoft.Azure.Devices;
using MQTTnet.Client.Publishing;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Rido.IoTHubClient.Tests
{
    public class HubMqttClientFixture
    {
        readonly RegistryManager rm;
        const string hubConnectionString = "HostName=tests.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=P5LfPNpLhLD/qJVOCTpuKXLi/9rmGqvkleB0quXxkws=";
        const string hubName = "tests.azure-devices.net";
        string deviceId = String.Empty;
        Device device;

        private readonly ITestOutputHelper output;

        public HubMqttClientFixture(ITestOutputHelper output)
        {
            this.output = output;
            // var tokenCredential = new DefaultAzureCredential();
            rm = RegistryManager.CreateFromConnectionString(hubConnectionString);
            deviceId = "testdevice" + new Random().Next(10);
            device = GetOrCreateDeviceAsync(deviceId).Result;
        }

        [Fact]
        public async Task ConnectWithCertKeyAndGetTwin()
        {
            await GetOrCreateDeviceAsync("testdevice", true);
            IHubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("testdevice.pfx", "1234"));
            Assert.True(client.IsConnected);

            var t = await client.GetTwinAsync();
            Assert.StartsWith("{", t);
            string expectedCS = $"HostName={hubName};DeviceId=testdevice;Auth=X509"; ;
            Assert.Equal(expectedCS, client.DeviceConnectionString.ToString());
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKey()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKeyAndSendTelemetry()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            var puback = await client.SendTelemetryAsync(new { temp = 2 });
            Assert.Equal(MqttClientPublishReasonCode.Success, puback.ReasonCode);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKeyAndSendTelemetryComponent()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            var puback = await client.SendTelemetryAsync(new { temp = 2 }, "mycomponent");
            Assert.Equal(MqttClientPublishReasonCode.Success, puback.ReasonCode);
            await client.CloseAsync();
        }

        [Fact]
        public async Task GetTwin()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var t = await client.GetTwinAsync();
            output.WriteLine(t);
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task UpdateTwin()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var tick = Environment.TickCount;
            var p = await client.UpdateTwinAsync(new { myProp = tick });

            output.WriteLine("PATCHED:" + p.ToString());

            await Task.Delay(2000);
            var twin = await rm.GetTwinAsync(deviceId);
            Assert.Contains(tick.ToString(), twin.ToJson());
            output.WriteLine(twin.ToJson());
            await client.CloseAsync();
        }

        [Fact]
        public async Task ReceivePropertyUpdate()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
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
            await Task.Delay(2000);
            Assert.True(propertyReceived);
            await client.CloseAsync();
        }


        [Fact]
        public async Task ReceiveCommand()
        {
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool commandInvoked = false;
            client.OnCommandReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Command {e.CommandName}");
                await client.CommandResponseAsync(e.Rid, e.CommandName, "200", new { myResponse = "ok" });
                commandInvoked = true;
            };

            ServiceClient sc = ServiceClient.CreateFromConnectionString(hubConnectionString);
            CloudToDeviceMethod c2dMethod = new("TestMethod");
            c2dMethod.SetPayloadJson(JsonSerializer.Serialize(new { myPayload = "some payload" }));
            var dmRes = await sc.InvokeDeviceMethodAsync(device.Id, c2dMethod);
            await Task.Delay(1000);
            Assert.True(commandInvoked);
            Assert.Equal("{\"myResponse\":\"ok\"}", dmRes.GetPayloadAsJson());
            await client.CloseAsync();
        }

        [Fact]
        public async Task AnnounceModelIdWithSaS()
        {
            string modelId = "dtmi:rido:test;1";
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey, modelId);
            var deviceRecord = await rm.GetTwinAsync(device.Id);
            Assert.Equal(modelId, deviceRecord.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task AnnounceModelIdWithX509()
        {
            string modelId = "dtmi:rido:test;1";
            IHubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("testdevice.pfx", "1234"), modelId);
            Assert.True(client.IsConnected);
            var deviceRecord = await rm.GetTwinAsync("testdevice");
            Assert.Equal(modelId, deviceRecord.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithSas()
        {
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            // TODO: without the named param, the overload does not get right
            IHubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, moduleId: module.Id, module.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithModelIDWithSas()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            IHubMqttClient client = await HubMqttClient.CreateAsync(
                hubName,
                device.Id,
                module.Id,
                module.Authentication.SymmetricKey.PrimaryKey,
                modelId);
            Assert.True(client.IsConnected);
            var twin = await rm.GetTwinAsync(module.DeviceId, module.Id);
            Assert.Equal(modelId, twin.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleDCSWithSas()
        {
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            IHubMqttClient client = await HubMqttClient.CreateFromConnectionStringAsync(
                $"HostName={hubName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey}");
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithModelIdDCSWithSas()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            IHubMqttClient client = await HubMqttClient.CreateFromConnectionStringAsync(
                $"HostName={hubName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};ModelId={modelId}");
            Assert.True(client.IsConnected);
            var twin = await rm.GetTwinAsync(module.DeviceId, module.Id);
            Assert.Equal(modelId, twin.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithCert()
        {
            await GetOrCreateModuleAsync("xd01", "xmod01", true);
            IHubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"));
            Assert.True(client.IsConnected);
            string expectedCS = $"HostName={hubName};DeviceId=xd01;ModuleId=xmod01;Auth=X509";
            Assert.Equal(expectedCS, client.DeviceConnectionString.ToString());
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithCertAndModelId()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync("xd01", "xmod01", true);
            IHubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"), modelId);
            Assert.True(client.IsConnected);

            var moduleTwin = await rm.GetTwinAsync(module.DeviceId, module.Id);
            Assert.Equal(modelId, moduleTwin.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectSameDeviceTwiceTriggersDisconnect()
        {
            var device= await GetOrCreateDeviceAsync("fakeDevice");
            var client1 = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client1.IsConnected);
            bool hasDisconnected = false;
            client1.OnMqttClientDisconnected += (o, e) => hasDisconnected = true;
            var client2 = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            Assert.True(hasDisconnected);
        }
        private async Task<Device> GetOrCreateDeviceAsync(string deviceId, bool x509 = false)
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
            output.WriteLine($"Test Device Created: {hubName} {device.Id}");
            return device;
        }

        private async Task<Module> GetOrCreateModuleAsync(string deviceId, string moduleId, bool x509 = false)
        {
            var device = await rm.GetDeviceAsync(deviceId);

            if (device == null)
            {
                await GetOrCreateDeviceAsync(deviceId, x509);
            }

            var module = await rm.GetModuleAsync(deviceId, moduleId);

            if (module == null)
            {
                module = new Module(deviceId, moduleId);
                if (x509)
                {
                    module.Authentication = new AuthenticationMechanism()
                    {
                        Type = AuthenticationType.CertificateAuthority
                    };
                }
                await rm.AddModuleAsync(module);
            }
            module = await rm.GetModuleAsync(deviceId, moduleId);
            output.WriteLine($"Created Module {module.Id} on {module.DeviceId}");
            return module;
        }
    }
}
