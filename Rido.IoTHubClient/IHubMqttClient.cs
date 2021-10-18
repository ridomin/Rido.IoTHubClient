using MQTTnet.Client.Publishing;
using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttClient
    {
        bool IsConnected { get; }

        event EventHandler<CommandEventArgs> OnCommandReceived;
        event EventHandler<PropertyEventArgs> OnPropertyReceived;

        Task CloseAsync();
        Task CommandResponseAsync(string rid, string cmdName, string status, object payload);
        Task<string> GetTwinAsync();
        Task<MqttClientPublishResult> SendTelemetryAsync(object payload);
        Task<int> UpdateTwinAsync(object payload);
    }
}