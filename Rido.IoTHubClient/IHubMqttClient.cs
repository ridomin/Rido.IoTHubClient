using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IHubMqttClient
    {
        IMqttConnection Connection { get; }
        Task<PubResult> SendTelemetryAsync(object payload, string componentName = "", CancellationToken cancellationToken = default);
        Task<string> GetTwinAsync(CancellationToken cancellationToken = default);
        Task<int> UpdateTwinAsync(object payload, CancellationToken cancellationToken = default);
        Func<PropertyReceived, Task<WritablePropertyAck>> OnPropertyChange { get; set; }
        Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
    }
}