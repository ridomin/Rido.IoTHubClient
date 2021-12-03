using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttClient
    {
        IMqttConnection Connection { get; }
        Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "");
        Task<string> GetTwinAsync();
        Task<int> UpdateTwinAsync(object payload);
        Func<PropertyReceived, Task<WritablePropertyAck>> OnPropertyChange { get; set; }
        Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
    }
}