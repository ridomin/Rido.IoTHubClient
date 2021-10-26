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
                SharedAccessKey = "sas",
                ModelId = "dtmi"
            };
            string expected = "DeviceId=d;HostName=h;SharedAccessKey=***;ModelId=dtmi;SasMinutes=60;RetryInterval=0;Auth=SAS";
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
            string expected = "DeviceId=d;HostName=h;ModuleId=m;SharedAccessKey=***;SasMinutes=60;RetryInterval=0;Auth=SAS";
            Assert.Equal(expected, dcs.ToString());
        }

        [Fact]
        public void DefaultValues()
        {
            var dcs = new DeviceConnectionString();
            Assert.Equal(60, dcs.SasMinutes);
        }
    }
}
