using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{

    public interface IHubMqttClient
    {
        bool IsConnected { get; }
        ConnectionSettings ConnectionSettings { get; }
        Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentName = "");
        Task<string> GetTwinAsync();
        Task<int> UpdateTwinAsync(object payload);
        event EventHandler<PropertyEventArgs> OnPropertyReceived;
        //event EventHandler<CommandEventArgs> OnCommandReceived;
        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        Task CommandResponseAsync(string rid, string cmdName, string status, object payload);
        Task CloseAsync();
    }

}