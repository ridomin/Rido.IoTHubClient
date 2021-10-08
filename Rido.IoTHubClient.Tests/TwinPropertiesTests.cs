using System;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class TwinPropertiesTests
    {

        [Fact]
        public void Ack()
        {
            var inJson = "{\"$version\":1,\"tool\":\"test\"}";
            var ack = TwinProperties.BuildAck(inJson, 1, 200, "ack description");
            Assert.Equal("{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":\"test\"}}", ack);
        }

        [Fact]
        public void Ack2()
        {
            var inJson = "{\"$version\":1,\"tool\":\"test\"}";
            var ack = TwinProperties.BuildAck(inJson, 1, 200, "ack description");
            Assert.Equal("{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":\"test\"}}", ack);
        }

        [Fact]
        public void RemoveDollarElements()
        {
            var inJson = "{\"$version\":1,\"tool\":\"test\"}";
            var outJson = TwinProperties.RemoveVersion(inJson);
            Assert.Equal("{\"tool\":\"test\"}", outJson);
        }
    }
}
