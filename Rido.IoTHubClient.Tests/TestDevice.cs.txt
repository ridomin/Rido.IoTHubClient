using MQTTnet.Client.Publishing;
using System;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient.Tests
{
    public class TestDevice
    {
        IMqttConnection connection;
        int lastRid = 0;

        public const int DefaultInterval = 7;

        Action<string> twin_cb;
        Action<int> patch_cb;

        public Func<WritableProperty<int>, Task<WritableProperty<int>>> OnProperty_interval_Updated = null;
        public WritableProperty<int> Property_interval { get; set; }

        private TestDevice(IMqttConnection conn)
        {
            this.connection = conn;
            this.connection.OnMessage = async m =>
            {
                await Task.Delay(1);
                var topic = m.ApplicationMessage.Topic;
                var segments = topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (topic.Contains("?"))
                {
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["$rid"]);
                    twinVersion = Convert.ToInt32(qs["$version"]);
                }
                string msg = string.Empty;
                if (m.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload);
                }

                if (topic.StartsWith("$iothub/twin/res/200"))
                {
                    twin_cb(msg);
                }

                if (topic.StartsWith("$iothub/twin/res/204"))
                {
                    patch_cb(twinVersion);
                }

                if (topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
                {
                    var root = JsonNode.Parse(msg);
                    await Invoke_interval_Callback(root);
                }
            };
        }

        private async Task Invoke_interval_Callback(JsonNode desired)
        {
            if (desired?["interval"] != null)
            {
                if (OnProperty_interval_Updated != null)
                {
                    var intervalProperty = new WritableProperty<int>("interval")
                    {
                        Value = Convert.ToInt32(desired?["interval"]?.GetValue<int>()),
                        Version = desired?["$version"]?.GetValue<int>() ?? 0
                    };
                    var ack = await OnProperty_interval_Updated.Invoke(intervalProperty);
                    if (ack != null)
                    {
                        Property_interval = ack;
                        _ = connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", ack.ToAck());
                    }
                }
            }
        }

        public static async Task<TestDevice> CreateTestDevice(string cs)
        {
            var conn = await HubMqttConnection.CreateAsync(new ConnectionSettings(cs) { ModelId = "dtmi:rido:pnp:memmon;1" });
            await conn.SubscribeAsync(new string[] {  
                        "$iothub/twin/res/#",
                        "$iothub/twin/PATCH/properties/desired/#"});
            return new TestDevice(conn);
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                patch_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                patch_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
        }

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                twin_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                twin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin GET: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
        }

        public async Task Init()
        {
            var twin = await GetTwinAsync();
            Property_interval = WritableProperty<int>.InitFromTwin(twin, "interval", DefaultInterval);
            if (OnProperty_interval_Updated != null && Property_interval.DesiredVersion > 1)
            {
                var ack = await OnProperty_interval_Updated?.Invoke(Property_interval);
                var v = await UpdateTwinAsync(ack.ToAck());
                Console.WriteLine("Updated ACK v: " + v);
                Property_interval = ack;
            }
            else
            {
                await UpdateTwinAsync(Property_interval.ToAck());
            }
        }
    }
}
