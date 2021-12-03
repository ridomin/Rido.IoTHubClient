using Rido.IoTHubClient.TopicBinders;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : BasicHubClient, IHubMqttClient
    {
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;

        AllCommandsBinder commandBinder;
        public Func<CommandRequest, Task<CommandResponse>> OnCommand 
        {
            get => commandBinder.OnCmdDelegate;
            set => commandBinder.OnCmdDelegate = value;
        }
        
        DesiredUpdateBinder desiredUpdate;
        public Func<PropertyReceived, Task<WritablePropertyAck>> OnPropertyChange 
        {
            get => desiredUpdate.OnProperty_Updated;
            set => desiredUpdate.OnProperty_Updated = value;
        }

        IMqttConnection IHubMqttClient.Connection => base.Connection;

        private bool disposedValue;

        public static async Task<IHubMqttClient> CreateAsync(string hostname, string deviceId, string deviceKey) =>
            await CreateAsync(new ConnectionSettings { HostName = hostname, DeviceId = deviceId, SharedAccessKey = deviceKey }, CancellationToken.None);
        public static async Task<IHubMqttClient> CreateAsync(string cs) => await CreateAsync(ConnectionSettings.FromConnectionString(cs), CancellationToken.None);
        public static async Task<IHubMqttClient> CreateAsync(string cs, CancellationToken cancellationToken) => await CreateAsync(ConnectionSettings.FromConnectionString(cs), cancellationToken);
        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs) => await CreateAsync(cs, CancellationToken.None);
        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs, CancellationToken cancellationToken)
        {
            var mqttConnection = await HubMqttConnection.CreateAsync(cs, cancellationToken);
            var hubClient = new HubMqttClient(mqttConnection);
            mqttConnection.OnMqttClientDisconnected += (o, e) => hubClient.OnMqttClientDisconnected?.Invoke(o, e);
            return hubClient;
        }

        private HubMqttClient(IMqttConnection conn) : base(conn)
        {
            desiredUpdate = new DesiredUpdateBinder(conn);
            commandBinder = new AllCommandsBinder(conn);
        }

        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await Connection.PublishAsync($"$iothub/methods/res/{status}/?$rid={rid}", payload);

        public Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "") => base.SendTelemetryAsync(payload);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Connection?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
