using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class TelemetryBinder<T>
    {
        readonly IMqttConnection connection;
        readonly string deviceId;
        string name;
        string componentName;
        public TelemetryBinder(IMqttConnection connection, string name,  string component = "")
        {
            this.connection = connection;
            this.deviceId = connection.ConnectionSettings.DeviceId;
            this.componentName = component;
            this.name = name;
        }
        public async Task<PubResult> SendTelemetryAsync(T payload, CancellationToken cancellationToken = default) => 
            await SendTelemetryAsync(payload, string.Empty, cancellationToken);

        public async Task<PubResult> SendTelemetryAsync(T payload, string componentName = "", CancellationToken cancellationToken = default)
        {
            this.componentName = componentName;
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

            Dictionary<string, T> typedPayload = new Dictionary<string, T>();
            typedPayload.Add(name, payload);
            var pubAck = await connection.PublishAsync(topic, typedPayload, cancellationToken);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

    }
}
