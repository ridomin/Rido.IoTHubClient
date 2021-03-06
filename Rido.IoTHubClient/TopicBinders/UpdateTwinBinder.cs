using MQTTnet.Client.Publishing;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class UpdateTwinBinder
    {
        readonly ConcurrentDictionary<int, TaskCompletionSource<int>> pendingRequests;
        readonly IMqttConnection connection;

        public UpdateTwinBinder(IMqttConnection connection)
        {
            pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<int>>();
            this.connection = connection;
            _ = connection.SubscribeAsync("$iothub/twin/res/#");
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;

                if (topic.StartsWith("$iothub/twin/res/204"))
                {
                    (int rid, int twinVersion) = TopicParser.ParseTopic(topic);
                    if (pendingRequests.TryRemove(rid, out var tcs))
                    {
                        tcs.SetResult(twinVersion);
                    }
                }
                await Task.Yield();
            };
        }

        public async Task<int> SendRequestWaitForResponse(object payload, int timeout = 5)
        {
            var rid = RidCounter.NextValue();
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={rid}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingRequests.TryAdd(rid, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback?.ReasonCode}' publishing twin GET");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(timeout));
        }
    }
}
