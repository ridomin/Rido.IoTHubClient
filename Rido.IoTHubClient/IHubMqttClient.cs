using MQTTnet;
using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttClient : IMqttConnection
    {
        Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "");
        Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        Task CommandResponseAsync(string rid, string cmdName, string status, object payload);
        Task<string> GetTwinAsync();
        Func<PropertyReceived, Task<WritablePropertyAck>> OnPropertyChange { get; set; }
        Task<int> UpdateTwinAsync(object payload);
    }
}