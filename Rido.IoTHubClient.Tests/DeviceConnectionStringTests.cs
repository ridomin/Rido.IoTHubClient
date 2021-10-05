
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

        // [Fact]
        // public void GetUserNameV2()
        // {
        //     DateTimeOffset expiry = DateTimeOffset.MinValue;
        //     var expiryString = expiry.ToUnixTimeMilliseconds().ToString();
        //     string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;SharedAccessKey=<SasKey>";
        //     DeviceConnectionString dcs = new DeviceConnectionString(cs);
        //     var username = dcs.GetUserName(expiryString);
        //     Assert.Equal("av=2021-06-30-preview&h=<hubname>.azure-devices.net&did=<deviceId>&am=SAS&se=-62135596800000", username);
        // }

        // [Fact]
        // public void BuildSasToken()
        // {
        //     DateTimeOffset expiry = DateTimeOffset.MinValue;
        //     var expiryString = expiry.ToUnixTimeMilliseconds().ToString();
        //     string cs = "HostName=<hubname>.azure-devices.net;DeviceId=<deviceId>;SharedAccessKey=MDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDA=";
        //     DeviceConnectionString dcs = new DeviceConnectionString(cs);
        //     var password = dcs.BuildSasToken(expiryString);
        //     Assert.Equal("NlC6BFxNoRyN1UGa2hLQMV/NbLlTbCEXamJawDBUcnw=", password);
        // }
    }
}
