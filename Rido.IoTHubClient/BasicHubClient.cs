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

        protected BasicHubClient(IMqttConnection c)
        {
            Connection = c;
            GetTwinBinder = new GetTwinBinder(Connection);
            UpdateTwinBinder = new UpdateTwinBinder(Connection);
        }

        public async Task<string> GetTwinAsync(CancellationToken cancellationToken = default) => await GetTwinBinder.GetTwinAsync(cancellationToken);

        public async Task<int> UpdateTwinAsync(object payload, CancellationToken cancellationToken = default) => await UpdateTwinBinder.UpdateTwinAsync(payload, cancellationToken);
    }
}
