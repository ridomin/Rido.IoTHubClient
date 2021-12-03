using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public  class AllCommandsBinder
    {
        public Func<CommandRequest, Task<CommandResponse>> OnCmdDelegate { get; set; }

        public AllCommandsBinder(IMqttConnection connection)
        {
            _ = connection.SubscribeAsync("$iothub/methods/POST/#");
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;

                if (topic.StartsWith($"$iothub/methods/POST/"))
                {
                    string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                    
                    if (OnCmdDelegate != null)
                    {
                        (int rid, _) = TopicParser.ParseTopic(topic);
                        var response = await OnCmdDelegate.Invoke(new CommandRequest
                        {
                            CommandName = topic.Split('/')[3],
                            CommandPayload = msg
                        }); ;
                        _ = connection.PublishAsync($"$iothub/methods/res/{response.Status}/?$rid={rid}", response.CommandResponsePayload);
                    }
                }
            };
        }
    }
}
