using Rido.IoTHubClient.TopicBinders;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class BasicHubClient
    {
        public IMqttConnection Connection;
        public string InitialTwin = string.Empty;

        public ConnectionSettings ConnectionSettings => Connection.ConnectionSettings;

        protected GetTwinBinder GetTwinBinder;
        protected UpdateTwinBinder UpdateTwinBinder;
        protected TelemetryBinder TelemetryBinder;

        protected BasicHubClient(IMqttConnection c)
        {
            Connection = c;
            GetTwinBinder = new GetTwinBinder(Connection);
            UpdateTwinBinder = new UpdateTwinBinder(Connection);
            TelemetryBinder = new TelemetryBinder(Connection, Connection.ConnectionSettings.DeviceId);
        }

        public async Task<string> GetTwinAsync(CancellationToken cancellationToken = default) => await GetTwinBinder.GetTwinAsync(cancellationToken);

        public async Task<int> UpdateTwinAsync(object payload, CancellationToken cancellationToken = default) => await UpdateTwinBinder.UpdateTwinAsync(payload, cancellationToken);

        public async Task<PubResult> SendTelemetryAsync(object payload, string componentName = "", CancellationToken cancellationToken = default) =>
            await TelemetryBinder.SendTelemetryAsync(payload, componentName, cancellationToken);
    }
}
