﻿//  <auto-generated/> 
#nullable enable

using MQTTnet.Client.Publishing;
using Rido.IoTHubClient;
using Rido.IoTHubClient.TopicBinders;

namespace dtmi_rido_pnp
{
    public class memmon : BasicHubClient
    {
        const string modelId = "dtmi:rido:pnp:memmon;1";

        public DateTime Property_started { get; private set; }

        public Bound_Property<bool> Property_enabled;
        public Bound_Property<int> Property_interval;
        public CommandBinder<Cmd_getRuntimeStats_Request, Cmd_getRuntimeStats_Response> Command_getRuntimeStats_Binder;

        private memmon(IMqttConnection c) : base(c)
        {
            Property_interval = new Bound_Property<int>(Connection, "interval");
            Property_enabled = new Bound_Property<bool>(Connection, "enabled");
            Command_getRuntimeStats_Binder = new CommandBinder<Cmd_getRuntimeStats_Request, Cmd_getRuntimeStats_Response>(Connection, "getRuntimeStats");
        }

        public static async Task<memmon> CreateDeviceClientAsync(string cs, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(cs);
            var connection = await HubMqttConnection.CreateAsync(new ConnectionSettings(cs) { ModelId = modelId }, cancellationToken);
            var client = new memmon(connection);
            client.InitialTwin = await client.GetTwinAsync();
            return client;
        }

        public async Task<int> Report_started_Async(DateTime started) => await UpdateTwinAsync(new { started });
        
        public async Task<PubResult> Send_workingSet_Async(double workingSet, CancellationToken cancellationToken = default(CancellationToken)) => 
            await base.SendTelemetryAsync(new { workingSet });
    }
}
