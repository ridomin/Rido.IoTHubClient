using MQTTnet;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IMqttConnection
    {
        ConnectionSettings ConnectionSettings { get; }
        bool IsConnected { get; }
        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        Task CloseAsync();

        Task<MqttClientSubscribeResult> SubscribeAsync(string[] topics);
        Task<MqttClientPublishResult> PublishAsync(string topic, object payload);
        Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessage { get; set; }
    }
}