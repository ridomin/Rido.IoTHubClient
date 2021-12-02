using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;

namespace pnp_memmon
{
    public class TelemetryBinder
    {
        IMqttConnection connection;
        string deviceId;
        public TelemetryBinder(IMqttConnection connection, string deviceId, string component = "")
        {
            this.connection = connection;
            this.deviceId = deviceId;
        }
        public async Task<MqttClientPublishResult> SendTelemetry(object payload, CancellationToken cancellationToken) =>
            await connection.PublishAsync($"devices/{deviceId}/messages/events/", payload, cancellationToken);
        

    }
}
