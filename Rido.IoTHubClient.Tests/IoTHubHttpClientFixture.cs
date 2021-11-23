using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class IoTHubHttpClientFixture
    {
        const string hostname = "tests.azure-devices.net";
        const string deviceId = "d8";
        static string DefaultKey => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N")));

        [Fact]
        public async Task SendTelemetryObject()
        {
            var client = new IoTHubHttpClient(new ConnectionSettings() { HostName = hostname, DeviceId = deviceId, SharedAccessKey = DefaultKey});
            var resp = await client.SendTelemetryAsync(new { temperature = 22 });
            Assert.True(resp.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SendTelemetryString()
        {
            var client = new IoTHubHttpClient(new ConnectionSettings() { HostName = hostname, DeviceId = deviceId, SharedAccessKey = DefaultKey });
            var resp = await client.SendTelemetryAsync("{ \"temperature\" = 22 }");
            Assert.True(resp.IsSuccessStatusCode);
        }
    }
}
