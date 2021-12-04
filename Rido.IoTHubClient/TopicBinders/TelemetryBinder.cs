using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class TelemetryBinder
    {
        readonly IMqttConnection connection;
        readonly string deviceId;
        string componentName;
        public TelemetryBinder(IMqttConnection connection, string component = "")
        {
            this.connection = connection;
            this.deviceId = connection.ConnectionSettings.DeviceId;
            this.componentName = component;
        }
        public async Task<PubResult> SendTelemetryAsync(object payload, string component = "", CancellationToken cancellationToken = default)
        {
            this.componentName = component;
            string topic = $"devices/{deviceId}";

            if (!string.IsNullOrEmpty(connection.ConnectionSettings.ModuleId))
            {
                topic += $"/modules/{connection.ConnectionSettings.ModuleId}";
            }
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(componentName))
            {
                topic += $"$.sub={componentName}";
            }
            var pubAck = await connection.PublishAsync(topic, payload, cancellationToken);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

    }
}
