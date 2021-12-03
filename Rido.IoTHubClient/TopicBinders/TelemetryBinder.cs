using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class TelemetryBinder
    {
        readonly IMqttConnection connection;
        readonly string deviceId;
        readonly string component;
        public TelemetryBinder(IMqttConnection connection, string component = "")
        {
            this.connection = connection;
            this.deviceId = connection.ConnectionSettings.DeviceId;
            this.component = component;
        }
        public async Task<PubResult> SendTelemetryAsync(object payload, CancellationToken cancellationToken = default)
        {
            string topic = $"devices/{deviceId}";

            if (!string.IsNullOrEmpty(connection.ConnectionSettings.ModuleId))
            {
                topic += $"/modules/{connection.ConnectionSettings.ModuleId}";
            }
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(component))
            {
                topic += $"$.sub={component}";
            }
            var pubAck = await connection.PublishAsync(topic, payload, cancellationToken);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

    }
}
