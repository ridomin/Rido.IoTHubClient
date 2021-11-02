using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class PropertiesAckFixture
    {
        [Fact]
        public void BuildAckFromSimpleValues()
        {
            var inJson = "{\"$version\":1,\"tool\":\"test\"}";
            
            var ack = new PropertyAck()
            {
                Description = "ack description",
                Status = 200,
                Version = 1,
                Value = inJson
            };
            var res = ack.BuildAck();
            Assert.NotNull(res);
            var expected = "{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":\"test\"}}";
            Assert.Equal(expected, res.ToString());
        }

        [Fact]
        public void BuildAckFromIntValues()
        {
            var inJson = "{\"$version\":1,\"tool\":2}";
            var ack = new PropertyAck()
            {
                Description = "ack description",
                Status = 200,
                Version = 1,
                Value = inJson
            };
            var res = ack.BuildAck();
            Assert.NotNull(res);
            var expected = "{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":2}}";
            Assert.Equal(expected, res.ToString());
        }

        [Fact]
        public void BuildAckFromDoubleValues()
        {
            var inJson = "{\"$version\":1,\"tool\":2.3}";
            var ack = new PropertyAck()
            {
                Description = "ack description",
                Status = 200,
                Version = 1,
                Value = inJson
            };
            var res = ack.BuildAck();
            Assert.NotNull(res);
            var expected = "{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":2.3}}";
            Assert.Equal(expected, res.ToString());
        }

        [Fact]
        public void BuildAckFromBooleanValues()
        {
            var inJson = "{\"$version\":1,\"tool\":false}";
            var ack = new PropertyAck()
            {
                Description = "ack description",
                Status = 200,
                Version = 1,
                Value = inJson
            };
            var res = ack.BuildAck();
            Assert.NotNull(res);
            var expected = "{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":false}}";
            Assert.Equal(expected, res.ToString());
        }

        [Fact]
        public void BuildAckFromComplexValues()
        {
            var inJson = "{\"$version\":1,\"tool\": { \"p1\" : 12 }}";
            var ack = new PropertyAck()
            {
                Description = "ack description",
                Status = 200,
                Version = 1,
                Value = inJson
            };
            var res = ack.BuildAck();
            Assert.NotNull(res);
            var expected = "{\"tool\":{\"ac\":200,\"av\":1,\"ad\":\"ack description\",\"value\":{\"p1\":12}}}";
            Assert.Equal(expected, res.ToString());
        }
    }
}
