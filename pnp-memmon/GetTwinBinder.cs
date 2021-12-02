using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace pnp_memmon
{
    internal class GetTwinBinder
    {
        ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        
        IMqttConnection connection;

        int lastRid =0;

        public GetTwinBinder(IMqttConnection conn)
        {
            connection = conn;
            connection.SubscribeAsync("$iothub/twin/res/#").Wait();
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;
                string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                (int rid, int twinVersion) = TopicParser.ParseTopic(topic);

                if (topic.StartsWith("$iothub/twin/res/200"))
                {
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
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync(string.Format($"$iothub/twin/GET/?$rid={lastRid}"), string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingGetTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback?.ReasonCode}' publishing twin GET");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(timeout));
        }

    }
}
