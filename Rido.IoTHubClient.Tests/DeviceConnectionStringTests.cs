
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class DeviceConnectionStringTests
    {
        [Fact]
        public void ParseConnectionString()
        {
            string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;SharedAccessKey=<SasKey>";
            DeviceConnectionString dcs = new DeviceConnectionString(cs);
            Assert.Equal("<hubname>.azure-devices.net", dcs.HostName);
            Assert.Equal("<deviceId>", dcs.DeviceId);
            Assert.Equal("<SasKey>", dcs.SharedAccessKey);
        }

        [Fact]
        public void ToStringReturnConnectionString()
        {
            DeviceConnectionString dcs = new DeviceConnectionString()
            {
                HostName = "h", DeviceId = "d", SharedAccessKey = "sas"
            };
            string expected = "HostName=h;DeviceId=d;SharedAccessKey=sas;Auth=SAS";
            Assert.Equal(expected, dcs.ToString());
        }
    }
}
