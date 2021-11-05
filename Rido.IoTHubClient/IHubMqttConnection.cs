using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttConnection
    {
        ConnectionSettings ConnectionSettings { get; }
        bool IsConnected { get; }
        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        Task CloseAsync();
    }
}