using Microsoft.Azure.Devices;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Rido.IoTHubClient.Tests
{
    public class HubMqttClientFixture
    {
        RegistryManager rm;
        string hubConnectionString = "HostName=tests.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=P5LfPNpLhLD/qJVOCTpuKXLi/9rmGqvkleB0quXxkws=";
        string hubName = "tests.azure-devices.net";
        string deviceId = "d" + new Random().Next(10);

        Device device;

        private readonly ITestOutputHelper output;

        public HubMqttClientFixture(ITestOutputHelper output)
        {
            // var tokenCredential = new DefaultAzureCredential();
            rm = RegistryManager.CreateFromConnectionString(hubConnectionString);
            device = GetOrCreateDeviceAsync(deviceId).Result;
            this.output = output;
        }

        [Fact]
        public async Task ConnectWithCertKeyAndGetTwin()
        {
            var xdevice = GetOrCreateDeviceAsync("testdevice", true);
            var client = await HubMqttClient.CreateWithClientCertsAsync(hubName, "testdevice.pfx", "1234");
            Assert.True(client.IsConnected);

            var t = await client.GetTwinAsync();
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKey()
        {
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task GetTwin()
        {
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var t = await client.GetTwinAsync();
            output.WriteLine(t);
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task UpdateTwin()
        {
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var tick = Environment.TickCount;
            var p = await client.UpdateTwinAsync(new { myProp = tick });

            output.WriteLine("PATCHED:" + p.ToString());

            await Task.Delay(2000);
            var twin = await rm.GetTwinAsync(deviceId);
            Assert.Contains(tick.ToString(), twin.ToJson());
            output.WriteLine(twin.ToJson());
        }

        [Fact]
        public async Task ReceivePropertyUpdate()
        {
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
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

            await Task.Delay(1000);
            Assert.True(propertyReceived);
            await client.CloseAsync();
        }


        [Fact]
        public async Task ReceiveCommand()
        {
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool commandInvoked = false;
            client.OnCommandReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Command {e.CommandName}");
                await client.CommandResponseAsync(e.Rid, e.CommandName, "200", new { myResponse = "ok" });
                commandInvoked = true;
            };
         
            ServiceClient sc = ServiceClient.CreateFromConnectionString(hubConnectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("TestMethod");
            c2dMethod.SetPayloadJson(JsonSerializer.Serialize(new { myPayload = "some payload" }));
            var dmRes =  await sc.InvokeDeviceMethodAsync(device.Id, c2dMethod);
            await Task.Delay(1000);
            Assert.True(commandInvoked);
            Assert.Equal("{\"myResponse\":\"ok\"}", dmRes.GetPayloadAsJson());
            await client.CloseAsync();
        }

        [Fact]
        public async Task AnnounceModelIdWithSaS()
        {
            string modelId = "dtmi:rido:test;1";
            var client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey, modelId);
            var deviceRecord = await rm.GetTwinAsync(device.Id);
            Assert.Equal(modelId, deviceRecord.ModelId);
        }

        [Fact]
        public async Task AnnounceModelIdWithX509()
        {
            string modelId = "dtmi:rido:test;1";
            var client = await HubMqttClient.CreateWithClientCertsAsync(hubName, "testdevice.pfx", "1234", modelId);
            Assert.True(client.IsConnected);
            var deviceRecord = await rm.GetTwinAsync(device.Id);
            Assert.Equal(modelId, deviceRecord.ModelId);
        }

        [Fact]
        public async Task ConnectModuleWithSas()
        {
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            var client = await HubMqttClient.CreateAsync(hubName, $"{device.Id}/{module.Id}", module.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public async Task ConnectModuleDCSWithSas()
        {
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            var client = await HubMqttClient.CreateFromConnectionStringAsync(
                $"HostName={hubName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey}");
            Assert.True(client.IsConnected);
        }

        //[Fact]
        //public async Task ConnectModuleWithCert()
        //{
        //    var module = await GetOrCreateModuleAsync(device.Id, "testmodule", true);
        //    var client = await HubMqttClient.CreateWithClientCertsAsync(hubName, @"C:\certs\ridocafy22\testmodule.pfx", "1234");
        //    Assert.True(client.IsConnected);
        //}


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
            Console.WriteLine($"Test Device Created: {hubName} {device.Id}");
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
            Console.WriteLine($"Created Module {module.Id} on {module.DeviceId}");
            return module;
        }
    }
}
