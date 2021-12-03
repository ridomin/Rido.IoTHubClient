using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class GetTwinBinder
    {
        readonly ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        readonly IMqttConnection connection;

        public GetTwinBinder(IMqttConnection conn)
        {
            connection = conn;
            _ = connection.SubscribeAsync("$iothub/twin/res/#");
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;

                if (topic.StartsWith("$iothub/twin/res/200"))
                {
                    string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                    (int rid, _) = TopicParser.ParseTopic(topic);
                     if (pendingGetTwinRequests.TryRemove(rid, out var tcs))
                    {
                        tcs.SetResult(msg);
                    }
                }
                await Task.Yield();
            };
        }

        public async Task<string> SendRequestWaitForResponse(int timeout = 5)
        {
            var rid = RidCounter.NextValue();
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync(string.Format($"$iothub/twin/GET/?$rid={rid}"), string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingGetTwinRequests.TryAdd(rid, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback?.ReasonCode}' publishing twin GET");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(timeout));
        }

    }
}
