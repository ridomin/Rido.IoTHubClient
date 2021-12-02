﻿//  <auto-generated/> 
#nullable enable

using MQTTnet.Client.Publishing;
using pnp_memmon;
using Rido.IoTHubClient;

namespace dtmi_rido_pnp
{
    public class memmon
    {
        const string modelId = "dtmi:rido:pnp:memmon;1";
        internal IMqttConnection _connection;
        string initialTwin = string.Empty;

        public ConnectionSettings ConnectionSettings => _connection.ConnectionSettings;

        private GetTwinBinder getTwinBinder;
        private UpdateTwinBinder updateTwinBinder;
        private TelemetryBinder telemetryBinder;

        public DateTime Property_started { get; private set; }

        public WritableProperty<bool>? Property_enabled;
        public DesiredUpdateTwinBinder<bool> Property_enabled_Desired;

        public WritableProperty<int>? Property_interval;
        public DesiredUpdateTwinBinder<int> Property_interval_Desired;
        
        public CommandBinder<Cmd_getRuntimeStats_Request, Cmd_getRuntimeStats_Response> Command_getRuntimeResponse_Binder;

        private memmon(IMqttConnection c)
        {
            _connection = c;
            getTwinBinder = new GetTwinBinder(_connection);
            updateTwinBinder = new UpdateTwinBinder(_connection);
            telemetryBinder = new TelemetryBinder(_connection, _connection.ConnectionSettings.DeviceId);
            Property_interval_Desired = new DesiredUpdateTwinBinder<int>(_connection, "interval");
            Property_enabled_Desired = new DesiredUpdateTwinBinder<bool>(_connection, "enabled");
            Command_getRuntimeResponse_Binder = new CommandBinder<Cmd_getRuntimeStats_Request, Cmd_getRuntimeStats_Response>(_connection, "getRuntimeStats");
        }

        public static async Task<memmon> CreateDeviceClientAsync(string cs, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(cs);
            var connection = await HubMqttConnection.CreateAsync(new ConnectionSettings(cs) { ModelId = modelId }, cancellationToken);
            var client = new memmon(connection);
            client.initialTwin = await client.GetTwinAsync();
            return client;
        }

        public async Task InitProperty_enabled_Async(bool defaultEnabled)
        {
            Property_enabled = WritableProperty<bool>.InitFromTwin(initialTwin, "enabled", defaultEnabled);
            if (Property_enabled_Desired.OnProperty_Updated != null && (Property_enabled.DesiredVersion > 1))
            {
                var ack = await Property_enabled_Desired.OnProperty_Updated.Invoke(Property_enabled);
                _ = UpdateTwinAsync(ack.ToAck());
                Property_enabled = ack;
            }
            else
            {
                _ = UpdateTwinAsync(Property_enabled.ToAck());
            }
        }

        public async Task InitProperty_interval_Async(int defaultInterval)
        {
            Property_interval = WritableProperty<int>.InitFromTwin(initialTwin, "interval", defaultInterval);
            if (Property_interval_Desired.OnProperty_Updated != null && (Property_interval.DesiredVersion > 1))
            {
                var ack = await Property_interval_Desired.OnProperty_Updated.Invoke(Property_interval);
                _ = UpdateTwinAsync(ack.ToAck());
                Property_interval = ack;
            }
            else
            {
                _ = UpdateTwinAsync(Property_interval.ToAck());
            }
        }

        public async Task<int> Report_started_Async(DateTime started) => await UpdateTwinAsync(new { started });
        
        public async Task<string> GetTwinAsync() => await getTwinBinder.SendRequestWaitForResponse();

        public async Task<int> UpdateTwinAsync(object payload) => await updateTwinBinder.SendRequestWaitForResponse(payload);
        
        public async Task<MqttClientPublishResult> Send_workingSet_Async(double workingSet) => await Send_workingSet_Async(workingSet, CancellationToken.None);
        public async Task<MqttClientPublishResult> Send_workingSet_Async(double workingSet, CancellationToken cancellationToken) => 
            await telemetryBinder.SendTelemetry(new { workingSet }, cancellationToken);
    }
}
