using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace pnp_memmon
{
    internal class BindRequestResponse
    {
        ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        
        IMqttConnection connection;
        string requestTopic;

        int lastRid =0;

        public BindRequestResponse(IMqttConnection conn, string requestTopic, string responseTopic)
        {
            this.requestTopic = requestTopic;
            connection = conn;
            connection.SubscribeAsync(responseTopic + "/#").Wait();
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;
                string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                (int rid, int twinVersion) = TopicParser.ParseTopic(topic);

                if (topic.StartsWith(responseTopic + "/200"))
                {
                     if (pendingGetTwinRequests.TryRemove(rid, out var tcs))
                    {
                        tcs.SetResult(msg);
                    }
                }
                await Task.Yield();
            };
        }

        public async Task<string> SendRequestWaitForResponse(int timeout)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync(string.Format(requestTopic, lastRid), string.Empty);
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
