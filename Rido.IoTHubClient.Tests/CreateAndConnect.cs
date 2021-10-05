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
    public class CreateAndConnect
    {

        private readonly ITestOutputHelper output;

        public CreateAndConnect(ITestOutputHelper output)
        {
            this.output = output;
        }

        static string hubName = "rido-freetier.azure-devices.net";
        static string deviceId = "testdevice3";
        [Fact]
        public async Task CreateDevice()
        {
           // var tokenCredential = new DefaultAzureCredential();
            var rm = RegistryManager.CreateFromConnectionString("HostName=rido-freetier.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=tMLbwroX2KsKH4PiHCcDKg31QOln4t29MVHIG2q6Ric=");

            var device = await rm.GetDeviceAsync(deviceId);
            if (device==null)
            {
                var d = new Device(deviceId);
                device = await rm.AddDeviceAsync(d);
            }
            Assert.True(device.Authentication.SymmetricKey.PrimaryKey.Length>0);
            var connectionString = $"HostName={hubName};DeviceId={deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";

            var client = await HubMqttClient.CreateFromConnectionStringAsync(connectionString);
            
            var t = await client.GetTwinAsync();
            output.WriteLine(t);
            
            var tick = Environment.TickCount;
            var p = await client.UpdateTwinAsync(new { myProp = tick });
            
            output.WriteLine("PATCHED:" + p.ToString());
            
            await Task.Delay(2000);
            var twin = await rm.GetTwinAsync(deviceId);
            Assert.Contains(tick.ToString(),twin.ToJson());
            output.WriteLine(twin.ToJson());

            bool propertyReceived = false;
            client.OnPropertyReceived += async (s, e) => {
                output.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                await Task.Delay(500);
                
                var ack = TwinProperties.BuildAck(e.PropertyMessageJson, e.Version, 200, "update ok");
                var v = await client.UpdateTwinAsync(ack);
                Console.WriteLine("PATCHED ACK: " + v);
                propertyReceived = true;
            };

            twin.Properties.Desired["myDProp"] = "some value";
            await rm.UpdateTwinAsync(deviceId, twin, twin.ETag);

            await Task.Delay(3000);
            Assert.True(propertyReceived);
        }
    }
}
