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

        protected async Task<string> GetTwinAsync() => await GetTwinBinder.SendRequestWaitForResponse();

        protected async Task<int> UpdateTwinAsync(object payload) => await UpdateTwinBinder.SendRequestWaitForResponse(payload);

        protected async Task<PubResult> SendTelemetryAsync(object payload) => await TelemetryBinder.SendTelemetryAsync(payload);
    }
}
