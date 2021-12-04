using Microsoft.Azure.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rido.IoTHubClient.Tests
{

    public class WritablePropertiesE2EFixture : IDisposable
    {
        const string hubConnectionString = "HostName=tests.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=P5LfPNpLhLD/qJVOCTpuKXLi/9rmGqvkleB0quXxkws=";
        const string hubName = "tests.azure-devices.net";
        readonly RegistryManager rm;
        readonly string deviceId = String.Empty;
        readonly Device device;

        private readonly ITestOutputHelper output;

        public WritablePropertiesE2EFixture(ITestOutputHelper output)
        {
            this.output = output;
            rm = RegistryManager.CreateFromConnectionString(hubConnectionString);
            deviceId = "memmon-test" + new Random().Next(100);
            output.WriteLine(deviceId);
            device = GetOrCreateDeviceAsync(deviceId).Result;
        }

        [Fact]
        public async Task NewDeviceSendDefaults()
        {
            var td = await TestDevice.CreateTestDevice($"HostName={hubName};DeviceId={deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}");
            await td.Init();

            var serviceTwin = await rm.GetTwinAsync(deviceId);

            var intervalTwin = serviceTwin.Properties.Reported["interval"];
            Assert.NotNull(intervalTwin);
            Assert.Equal(TestDevice.DefaultInterval, Convert.ToInt32(intervalTwin["value"]));
            Assert.Equal(0, Convert.ToInt32(intervalTwin["av"]));
            Assert.Equal(201, Convert.ToInt32(intervalTwin["ac"]));
            Assert.Equal("Init from default value", Convert.ToString(intervalTwin["ad"]));
            Assert.Equal(TestDevice.DefaultInterval, td.Property_interval.Value);
        }

        [Fact]
        public async Task DeviceReadsSettingsAtStartup()
        {
            var twin = await rm.GetTwinAsync(deviceId);
            int interval = 5;
            var patch = new
            {
                properties = new
                {
                    desired = new
                    {
                        interval
                    }
                }
            };
            await rm.UpdateTwinAsync(deviceId, JsonSerializer.Serialize(patch), twin.ETag);

            var td = await TestDevice.CreateTestDevice($"HostName={hubName};DeviceId={deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}");
            td.OnProperty_interval_Updated = async m =>
            {
                return await Task.FromResult(new PropertyAck<int>("interval")
                {
                    Version = m.Version,
                    Value = m.Value,
                    Status = 200,
                    Description = "accepted from device"
                });
            };
            
            await td.Init();

            var serviceTwin = await rm.GetTwinAsync(deviceId);

            var intervalTwin = serviceTwin.Properties.Reported["interval"];
            Assert.NotNull(intervalTwin);
            Assert.Equal(interval, Convert.ToInt32(intervalTwin["value"]));
            Assert.Equal(serviceTwin.Properties.Desired.Version, Convert.ToInt32(intervalTwin["av"]));
            Assert.Equal(200, Convert.ToInt32(intervalTwin["ac"]));
            Assert.Equal("accepted from device", Convert.ToString(intervalTwin["ad"]));
            Assert.Equal("accepted from device", td.Property_interval.Description);
            Assert.Equal(interval, td.Property_interval.Value);
        }

        [Fact]
        public async Task UpdatesDesiredPropertyWhenOnline()
        {
            var td = await TestDevice.CreateTestDevice($"HostName={hubName};DeviceId={deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}");
            td.OnProperty_interval_Updated = async m =>
            {
                return await Task.FromResult(new PropertyAck<int>("interval")
                {
                    Version = m.Version,
                    Value = m.Value,
                    Status = 200,
                    Description = "accepted from device"
                });
            };

            await td.Init();

            var serviceTwin = await rm.GetTwinAsync(deviceId);
            var intervalTwin = serviceTwin.Properties.Reported["interval"];
            Assert.NotNull(intervalTwin);
            Assert.Equal(TestDevice.DefaultInterval, Convert.ToInt32(intervalTwin["value"]));
            Assert.Equal(0, Convert.ToInt32(intervalTwin["av"]));
            Assert.Equal(201, Convert.ToInt32(intervalTwin["ac"]));
            Assert.Equal("Init from default value", Convert.ToString(intervalTwin["ad"]));
            Assert.Equal(TestDevice.DefaultInterval, td.Property_interval.Value);


            var twin = await rm.GetTwinAsync(deviceId);
            int interval = 9;
            var patch = new
            {
                properties = new
                {
                    desired = new
                    {
                        interval
                    }
                }
            };
            await rm.UpdateTwinAsync(deviceId, JsonSerializer.Serialize(patch), twin.ETag);

            await Task.Delay(500);


            serviceTwin = await rm.GetTwinAsync(deviceId);
            intervalTwin = serviceTwin.Properties.Reported["interval"];
            Assert.NotNull(intervalTwin);
            Assert.Equal(interval, Convert.ToInt32(intervalTwin["value"]));
            Assert.Equal(serviceTwin.Properties.Desired.Version, Convert.ToInt32(intervalTwin["av"]));
            Assert.Equal(200, Convert.ToInt32(intervalTwin["ac"]));
            Assert.Equal("accepted from device", Convert.ToString(intervalTwin["ad"]));
            Assert.Equal("accepted from device", td.Property_interval.Description);
            Assert.Equal(interval, td.Property_interval.Value);
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

        public void Dispose()
        {
            rm.RemoveDeviceAsync(deviceId).Wait();
        }
    }
}
