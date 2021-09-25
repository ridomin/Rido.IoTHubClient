using System;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class TwinPropertiesTests
    {
        [Fact]
        public void RemoveDollarElements()
        {
            var inJson = "{\"$version\":1,\"tool\":\"test\"}";
            var outJson = TwinProperties.RemoveVersion(inJson);
            Assert.Equal("{\"tool\":\"test\"}", outJson);
        }
    }
}
