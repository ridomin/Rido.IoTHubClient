using Rido.IoTHubClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class CommandBinder<T, TResponse> 
        where T : IBaseCommandRequest, new()
        where TResponse : BaseCommandResponse
    {
        public Func<T, Task<TResponse>> OnCmdDelegate { get; set; }

        public CommandBinder(IMqttConnection connection, string commandName, string componentName = "")
        {
            _ = connection.SubscribeAsync("$iothub/methods/POST/#");
            connection.OnMessage += async m =>
            {
                var topic = m.ApplicationMessage.Topic;

                var fullCommandName = string.IsNullOrEmpty(componentName) ? commandName : $"{componentName}*{commandName}";

                if (topic.StartsWith($"$iothub/methods/POST/{fullCommandName}"))
                {
                    string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                    T req = (T)new T().DeserializeBody(msg);
                    if (OnCmdDelegate != null && req != null )
                    {
                        (int rid, _) = TopicParser.ParseTopic(topic);
                        TResponse response = await OnCmdDelegate.Invoke(req);
                        _ = connection.PublishAsync($"$iothub/methods/res/{response.Status}/?$rid={rid}", response);
                    }
                }
            };
        }
    }
}
