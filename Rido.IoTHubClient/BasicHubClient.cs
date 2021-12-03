using Rido.IoTHubClient.TopicBinders;
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

        public async Task<string> GetTwinAsync() => await GetTwinBinder.SendRequestWaitForResponse();

        public async Task<int> UpdateTwinAsync(object payload) => await UpdateTwinBinder.SendRequestWaitForResponse(payload);

        public async Task<PubResult> SendTelemetryAsync(object payload) => await TelemetryBinder.SendTelemetryAsync(payload);
    }
}
