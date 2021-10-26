using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class ConnectionSettingsFixture
    {
        [Fact]
        public void ParseConnectionString()
        {
            string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;SharedAccessKey=<SasKey>";
            ConnectionSettings dcs = ConnectionSettings.FromConnectionString(cs);
            Assert.Equal("<hubname>.azure-devices.net", dcs.HostName);
            Assert.Equal("<deviceId>", dcs.DeviceId);
            Assert.Equal("<SasKey>", dcs.SharedAccessKey);
        }

        [Fact]
        public void ParseConnectionStringWithModule()
        {
            string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;ModuleId=<moduleId>;SharedAccessKey=<SasKey>";
            ConnectionSettings dcs = ConnectionSettings.FromConnectionString(cs);
            Assert.Equal("<hubname>.azure-devices.net", dcs.HostName);
            Assert.Equal("<deviceId>", dcs.DeviceId);
            Assert.Equal("<moduleId>", dcs.ModuleId);
            Assert.Equal("<SasKey>", dcs.SharedAccessKey);
        }

        [Fact]
        public void ToStringReturnConnectionString()
        {
            ConnectionSettings dcs = new()
            {
                HostName = "h",
                DeviceId = "d",
                SharedAccessKey = "sas",
                ModelId = "dtmi"
            };
            string expected = "DeviceId=d;HostName=h;SharedAccessKey=***;ModelId=dtmi;SasMinutes=60;RetryInterval=5;MaxRetries=10;Auth=SAS";
            Assert.Equal(expected, dcs.ToString());
        }

        [Fact]
        public void ToStringReturnConnectionStringWithModule()
        {
            ConnectionSettings dcs = new()
            {
                HostName = "h",
                DeviceId = "d",
                ModuleId = "m",
                SharedAccessKey = "sas"
            };
            string expected = "DeviceId=d;HostName=h;ModuleId=m;SharedAccessKey=***;SasMinutes=60;RetryInterval=5;MaxRetries=10;Auth=SAS";
            Assert.Equal(expected, dcs.ToString());
        }

        [Fact]
        public void DefaultValues()
        {
            var dcs = new ConnectionSettings();
            Assert.Equal(60, dcs.SasMinutes);
            Assert.Equal(5, dcs.RetryInterval);
            Assert.Equal(10, dcs.MaxRetries);
            Assert.Equal("SAS", dcs.Auth);
        }
    }
}
