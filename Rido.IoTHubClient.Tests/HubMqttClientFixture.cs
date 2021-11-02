using Microsoft.Azure.Devices;
using MQTTnet.Client.Publishing;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Rido.IoTHubClient.Tests
{
    public class HubMqttClientFixture
    {
        readonly RegistryManager rm;
        const string hubConnectionString = "HostName=broker.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=HbdIWLOaSHdaL5xmF0OhiC0kmDHPinOyI0kISxZ0Rt0=";
        const string hubName = "broker.azure-devices.net";
        readonly string deviceId = String.Empty;
        readonly Device device;

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
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("testdevice.pfx", "1234"));
            Assert.True(client.IsConnected);

            var t = await client.GetTwinAsync();
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectWithSasKey()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetry_SaS()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var puback = await client.SendTelemetryAsync(new { temp = 2 });
            Assert.Equal(PubResult.Success, puback);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetry_SaSModule()
        {
            var module = await GetOrCreateModuleAsync("deviceWithModules", "ModuleSas");
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, module.DeviceId, module.Id, module.Authentication.SymmetricKey.PrimaryKey, string.Empty);
            var puback = await client.SendTelemetryAsync(new { temp = 2 });
            Assert.Equal(PubResult.Success, puback);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetry_SaSModule_PnP()
        {
            var module = await GetOrCreateModuleAsync("deviceWithModules", "ModuleSas");
            var modelId = "dtmi:test:module;1";
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, module.DeviceId, module.Id, module.Authentication.SymmetricKey.PrimaryKey, modelId);
            var puback = await client.SendTelemetryAsync(new { temp = 2 }, "comp1");
            Assert.Equal(PubResult.Success, puback);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetry_X509Module()
        {
            var module = await GetOrCreateModuleAsync("xd01", "xmod01", true);
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"));
            var puback = await client.SendTelemetryAsync(new { temp = 2 });
            Assert.Equal(PubResult.Success, puback);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetry_X509Module_PnP()
        {
            await GetOrCreateModuleAsync("xd01", "xmod01", true);
            var modelId = "dtmi:test:module;1";
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"), modelId);
            var puback = await client.SendTelemetryAsync(new { temp = 2 }, "comp1");
            Assert.Equal(PubResult.Success, puback);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task SendTelemetryComponent_SaS()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            var puback = await client.SendTelemetryAsync(new { temp = 2 }, "mycomponent");
            Assert.Equal(PubResult.Success, puback);
            await client.CloseAsync();
        }

        [Fact]
        public async Task GetTwin()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var t = await client.GetTwinAsync();
            output.WriteLine(t);
            Assert.StartsWith("{", t);
            await client.CloseAsync();
        }

        [Fact]
        public async Task UpdateTwin()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
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
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool propertyReceived = false;
            var updatedVersion = 0;
            client.OnPropertyChange = e =>
            {
                output.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                propertyReceived = true;
                updatedVersion = e.Version;
                return new PropertyAck
                {
                    Description = "test update",
                    Status = 200,
                    Version = e.Version,
                    Value = e.PropertyMessageJson
                };
            };
            var twin = await rm.GetTwinAsync(deviceId);
            twin.Properties.Desired["myDProp"] = 2;
            await rm.UpdateTwinAsync(deviceId, twin, twin.ETag);
            await Task.Delay(2000);
            Assert.True(propertyReceived);

            var updatedTwin = await rm.GetTwinAsync(deviceId);
            Microsoft.Azure.Devices.Shared.TwinCollection updatedProp = updatedTwin.Properties.Reported["myDProp"];

            var expAck = new
            {
                ac = 200,
                ad = "test update",
                av = updatedVersion,
                value = 2
            };
            Assert.Equal(JsonSerializer.Serialize(expAck), updatedProp.ToJson());

            await client.CloseAsync();
        }

        [Fact]
        public async Task ReceivePropertyUpdateWithComplexObject()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool propertyReceived = false;
            var updatedVersion = 0;
            client.OnPropertyChange = e =>
            {
                output.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                propertyReceived = true;
                updatedVersion = e.Version;
                return new PropertyAck
                {
                    Description = "test update",
                    Status = 200,
                    Version = e.Version,
                    Value = e.PropertyMessageJson
                };
            };
            var twin = await rm.GetTwinAsync(deviceId);
            twin.Properties.Desired["myDProp"] = new { aComplexPerson = new { withName = "rido" } };
            await rm.UpdateTwinAsync(deviceId, twin, twin.ETag);
            await Task.Delay(2000);
            Assert.True(propertyReceived);

            var updatedTwin = await rm.GetTwinAsync(deviceId);
            Microsoft.Azure.Devices.Shared.TwinCollection updatedProp = updatedTwin.Properties.Reported["myDProp"];

            var expAck = new
            {
                ac = 200,
                av = updatedVersion,
                ad = "test update",
                value = new { aComplexPerson = new { withName = "rido" } }
            };
            Assert.Equal(JsonSerializer.Serialize(expAck), updatedProp.ToJson());

            await client.CloseAsync();
        }


        [Fact]
        public async Task ReceiveCommand()
        {
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            bool commandInvoked = false;

            client.OnCommand = req =>
            {
                Console.WriteLine($"Processing Command {req.CommandName}");
                commandInvoked = true;
                return new CommandResponse()
                {
                    _status = 200,
                    CommandResponsePayload = new { myResponse = "ok" }
                };
            };

            int retries = 0;
            while (!client.IsConnected && retries++ < 10)
            {
                output.WriteLine(client.IsConnected.ToString());
                await Task.Delay(100);
            }

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
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey, modelId);
            var deviceRecord = await rm.GetTwinAsync(device.Id);
            Assert.Equal(modelId, deviceRecord.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task AnnounceModelIdWithX509()
        {
            string modelId = "dtmi:rido:test;1";
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("testdevice.pfx", "1234"), modelId);
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
            HubMqttClient client = await HubMqttClient.CreateAsync(hubName, device.Id, moduleId: module.Id, module.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithModelIDWithSas()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            HubMqttClient client = await HubMqttClient.CreateAsync(
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
            HubMqttClient client = await HubMqttClient.CreateFromConnectionStringAsync(
                $"HostName={hubName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey}");
            Assert.True(client.IsConnected);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithModelIdDCSWithSas()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync(device.Id, "moduleOne");
            HubMqttClient client = await HubMqttClient.CreateFromConnectionStringAsync(
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
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"));
            Assert.True(client.IsConnected);
            string expectedCS = $"HostName={hubName};DeviceId=xd01;ModuleId=xmod01;Auth=X509";
            Assert.Equal(expectedCS, client.ConnectionSettings.ToString());
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectModuleWithCertAndModelId()
        {
            string modelId = "dtmi:rido:tests;1";
            var module = await GetOrCreateModuleAsync("xd01", "xmod01", true);
            HubMqttClient client = await HubMqttClient.CreateWithClientCertsAsync(hubName, new X509Certificate2("xd01_xmod01.pfx", "1234"), modelId);
            Assert.True(client.IsConnected);

            var moduleTwin = await rm.GetTwinAsync(module.DeviceId, module.Id);
            Assert.Equal(modelId, moduleTwin.ModelId);
            await client.CloseAsync();
        }

        [Fact]
        public async Task ConnectSameDeviceTwiceTriggersDisconnect()
        {
            var device = await GetOrCreateDeviceAsync("fakeDevice");
            var client1 = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client1.IsConnected);
            bool hasDisconnected = false;
            client1.OnMqttClientDisconnected += (o, e) => hasDisconnected = true;
            var client2 = await HubMqttClient.CreateAsync(hubName, device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            Assert.True(client2.IsConnected);
            await Task.Delay(300);
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
                else
                {
                    module.Authentication = new AuthenticationMechanism()
                    {
                        Type = AuthenticationType.Sas,
                        SymmetricKey = new SymmetricKey()
                        {
                            PrimaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N"))),
                            SecondaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N")))
                        }
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
