using System;
using System.Text;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class DesiredUpdateBinder
    {
        public Func<PropertyReceived, Task<WritablePropertyAck>> OnProperty_Updated = null;
        public DesiredUpdateBinder(IMqttConnection connection)
        {
            _ = connection.SubscribeAsync("$iothub/twin/PATCH/properties/desired/#");
            UpdateTwinBinder updateTwin = new UpdateTwinBinder(connection);
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;
                if (topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
                {
                    string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                    (int rid, int twinVersion) = TopicParser.ParseTopic(topic);
                    if (OnProperty_Updated != null)
                    {
                        var ack = await OnProperty_Updated.Invoke(new PropertyReceived
                        {
                            PropertyMessageJson = msg,
                            Rid = rid.ToString(),
                            Topic = topic,
                            Version = twinVersion
                        });
                        if (ack != null)
                        {
                            _ = updateTwin.SendRequestWaitForResponse(ack.ToAck());
                        }
                    }
                }
            };
        }
    }
}
