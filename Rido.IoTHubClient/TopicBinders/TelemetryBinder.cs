using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class TelemetryBinder
    {
        IMqttConnection connection;
        string deviceId;
        public TelemetryBinder(IMqttConnection connection, string component = "")
        {
            this.connection = connection;
            this.deviceId = connection.ConnectionSettings.DeviceId;
        }
        public async Task<MqttClientPublishResult> SendTelemetry(object payload, CancellationToken cancellationToken) =>
            await connection.PublishAsync($"devices/{deviceId}/messages/events/", payload, cancellationToken);

    }
}
