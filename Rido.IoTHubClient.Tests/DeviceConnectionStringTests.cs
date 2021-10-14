
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
        public void ParseConnectionStringWithModule()
        {
            string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;ModuleId=<moduleId>;SharedAccessKey=<SasKey>";
            DeviceConnectionString dcs = new DeviceConnectionString(cs);
            Assert.Equal("<hubname>.azure-devices.net", dcs.HostName);
            Assert.Equal("<deviceId>", dcs.DeviceId);
            Assert.Equal("<moduleId>", dcs.ModuleId);
            Assert.Equal("<SasKey>", dcs.SharedAccessKey);
        }

        [Fact]
        public void ToStringReturnConnectionString()
        {
            DeviceConnectionString dcs = new DeviceConnectionString()
            {
                HostName = "h",
                DeviceId = "d",
                SharedAccessKey = "sas"
            };
            string expected = "HostName=h;DeviceId=d;SharedAccessKey=sas;Auth=SAS;ModelId=dtmi";
            Assert.Equal(expected, dcs.ToString());
        }

        [Fact]
        public void ToStringReturnConnectionStringWithModule()
        {
            DeviceConnectionString dcs = new DeviceConnectionString()
            {
                HostName = "h",
                DeviceId = "d",
                ModuleId = "m",
                SharedAccessKey = "sas"
            };
            string expected = "HostName=h;DeviceId=d;ModuleId=m;SharedAccessKey=sas;Auth=SAS";
            Assert.Equal(expected, dcs.ToString());
        }
    }
}
