using Rido.IoTHubClient.TopicBinders;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : BasicHubClient, IHubMqttClient
    {
        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;

        readonly AllCommandsBinder commandBinder;
        public Func<CommandRequest, Task<CommandResponse>> OnCommand
        {
            get => commandBinder.OnCmdDelegate;
            set => commandBinder.OnCmdDelegate = value;
        }

        readonly DesiredUpdateBinder desiredUpdate;
        public Func<PropertyReceived, Task<WritablePropertyAck>> OnPropertyChange
        {
            get => desiredUpdate.OnProperty_Updated;
            set => desiredUpdate.OnProperty_Updated = value;
        }
        IMqttConnection IHubMqttClient.Connection => base.Connection;

        public static async Task<IHubMqttClient> CreateAsync(string cs, CancellationToken cancellationToken = default) =>
            await CreateAsync(ConnectionSettings.FromConnectionString(cs), cancellationToken);

        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs, CancellationToken cancellationToken = default)
        {
            var mqttConnection = await HubMqttConnection.CreateAsync(cs, cancellationToken);
            return new HubMqttClient(mqttConnection);
        }

        private HubMqttClient(IMqttConnection conn) : base(conn)
        {
            conn.OnMqttClientDisconnected += (o, e) => OnMqttClientDisconnected?.Invoke(o, e);
            desiredUpdate = new DesiredUpdateBinder(conn);
            commandBinder = new AllCommandsBinder(conn);
        }

        private bool disposedValue;
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
