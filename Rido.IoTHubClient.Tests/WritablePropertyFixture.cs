using System;
using System.Collections.Generic;
using Xunit;

namespace Rido.IoTHubClient.Tests
{
    public class WritablePropertyFixture
    {
        static string js(object o) => System.Text.Json.JsonSerializer.Serialize(o);

        [Fact]
        public void InitEmptyTwin()
        {
            string twin = js(new
            {
                reported = new Dictionary<string, object>() { { "$version", 1 } },
                desired = new Dictionary<string, object>() { { "$version", 1 } },
            });

            WritableProperty<double> twinProp = WritableProperty<double>.InitFromTwin(twin, "myProp", 0.2);
            Assert.Equal(0.2, twinProp.Value);
            Assert.Equal(0, twinProp.Version);
            Assert.Equal(201, twinProp.Status);
        }

        [Fact]
        public void InitTwinWithReported()
        {
            string twin = js(new
            {
                reported = new
                {
                    myProp = new
                    {
                        ac = 201,
                        av = 1,
                        value = 4.3
                    }
                },
                desired = new Dictionary<string, object>() { { "$version", 1 } },
            });

            WritableProperty<double> twinProp = WritableProperty<double>.InitFromTwin(twin, "myProp", 0.2);
            Assert.Equal(4.3, twinProp.Value);
            Assert.Equal(1, twinProp.Version);
            Assert.Equal(201, twinProp.Status);
        }

        [Fact]
        public void InitTwinWithDesired()
        {
            string twin = js(new
            {
                reported = new Dictionary<string, object>() { { "$version", 1 } },
                desired = new Dictionary<string, object>() { { "$version", 2 }, { "myProp", 3.1 } },
            });

            WritableProperty<double> twinProp = WritableProperty<double>.InitFromTwin(twin, "myProp", 0.2);
            Assert.Equal(3.1, twinProp.Value);
            Assert.Equal(2, twinProp.DesiredVersion);
        }

        [Fact]
        public void AckDouble()
        {
            var wp = new WritableProperty<double>("aDouble")
            {
                Description = "updated",
                Status = 200,
                Version = 3,
                Value = 1.2,
            };

            var expectedJson = js(new
            {
                aDouble = new
                {
                    av = 3,
                    ad = "updated",
                    ac = 200,
                    value = 1.2,
                }
            });
            Assert.Equal(expectedJson, wp.ToAck());
        }

        [Fact]
        public void AckDateTime()
        {
            var wp = new WritableProperty<DateTime>("aDateTime")
            {
                Value = new DateTime(2011, 11, 10, 8, 31, 12),
                Version = 3,
                Status = 200,
                Description = "updated"
            };

            var expectedJson = js(new
            {
                aDateTime = new
                {
                    av = 3,
                    ad = "updated",
                    ac = 200,
                    value = "2011-11-10T08:31:12",
                }
            });
            Assert.Equal(expectedJson, wp.ToAck());
        }

        [Fact]
        public void AckBool()
        {
            var wp = new WritableProperty<bool>("aBoolean")
            {
                Value = false,
                Version = 3,
                Status = 200,
                Description = "updated"
            };

            var expectedJson = js(new
            {
                aBoolean = new
                {
                    av = 3,
                    ad = "updated",
                    ac = 200,
                    value = false,
                }
            });
            Assert.Equal(expectedJson, wp.ToAck());
        }

        [Fact]
        public void AckComplexOject()
        {
            var aComplexObj = new AComplexObj() { AIntProp = 1 , AStringProp ="a"};
            var prop = new WritableProperty<AComplexObj>("aComplexObj")
            {
                Version = 3,
                Value = aComplexObj,
                Status = 213,
                Description = "description"
            };
            var expectedJson = js(new
            {
                aComplexObj = new
                {
                    av = 3,
                    ad = "description",
                    ac = 213,
                    value = new
                    {
                        AStringProp ="a",
                        AIntProp = 1
                    },
                }
            });
            Assert.Equal(expectedJson, prop.ToAck());
        }

    }
    class AComplexObj
    {
        public string AStringProp { get; set; }
        public int AIntProp { get; set; }

    }
}
