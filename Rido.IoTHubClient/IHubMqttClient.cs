using MQTTnet;
using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttClient
    {
        ConnectionSettings ConnectionSettings { get; }
        bool IsConnected { get; }
        Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        Action<MqttApplicationMessageReceivedEventArgs> OnMessage { get; set; }
        Func<PropertyReceived, Task<PropertyAck>> OnPropertyChange { get; set; }

        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;

        Task CommandResponseAsync(string rid, string cmdName, string status, object payload);
        Task<string> GetTwinAsync();
        Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "");
        Task<int> UpdateTwinAsync(object payload);
    }
}