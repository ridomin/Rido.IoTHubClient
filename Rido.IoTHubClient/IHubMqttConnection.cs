using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttConnection
    {
        ConnectionSettings ConnectionSettings { get; }
        bool IsConnected { get; }
        // Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        // Func<PropertyReceived, Task<PropertyAck>> OnPropertyChange { get; set; }
        //public bool reconnecting { get; }
        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        Task CloseAsync();
        IMqttClient MqttClient { get; }
        Task<MqttClientPublishResult> PublishAsync(string topic, object payload);
        //Task CommandResponseAsync(string rid, string cmdName, string status, object payload);
        // void Dispose();
        //Task<string> GetTwinAsync();
        //Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "");
        //Task<int> UpdateTwinAsync(object payload);
    }
}