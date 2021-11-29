using MQTTnet;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : IHubMqttClient
    {
        public ConnectionSettings ConnectionSettings => connection.ConnectionSettings;
        public bool IsConnected => connection.IsConnected;

        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        public Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        public Func<PropertyReceived, Task<PropertyAck>> OnPropertyChange { get; set; }

        const int twinOperationTimeoutSeconds = 55;

        ConcurrentDictionary<int, TaskCompletionSource<string>> pendingGetTwinRequests = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        ConcurrentDictionary<int, TaskCompletionSource<int>> pendingUpdateTwinRequests = new  ConcurrentDictionary<int, TaskCompletionSource<int>>();

        int lastRid = 0;
        private bool disposedValue;

        readonly IMqttConnection connection;

        public static async Task<IHubMqttClient> CreateAsync(string hostname, string deviceId, string deviceKey) => 
            await CreateAsync(new ConnectionSettings { HostName = hostname, DeviceId = deviceId, SharedAccessKey = deviceKey }, CancellationToken.None);

        public static async Task<IHubMqttClient> CreateAsync(string cs) => await CreateAsync(ConnectionSettings.FromConnectionString(cs), CancellationToken.None);
        public static async Task<IHubMqttClient> CreateAsync(string cs, CancellationToken cancellationToken) => await CreateAsync(ConnectionSettings.FromConnectionString(cs), cancellationToken);
        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs) => await CreateAsync(cs, CancellationToken.None);
        public static async Task<IHubMqttClient> CreateAsync(ConnectionSettings cs, CancellationToken cancellationToken)
        {
            var mqttConnection = await HubMqttConnection.CreateAsync(cs, cancellationToken);
            var hubClient = new HubMqttClient(mqttConnection);
            hubClient.ConfigureReservedTopics();
            mqttConnection.OnMqttClientDisconnected += (o, e) => hubClient.OnMqttClientDisconnected?.Invoke(o, e);
            return hubClient;
        }

        private HubMqttClient(IMqttConnection conn)
        {
            connection = conn;
            connection.OnMessage = m => OnMessage(m);
        }

        public async Task CloseAsync()
        {
            await connection.CloseAsync();
        }

        public async Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "")
        {
            string topic = $"$az/iot/telemetry";

            if (!string.IsNullOrEmpty(dtdlComponentname))
            {
                topic += $"/?dts={dtdlComponentname}";
            }
            var pubAck = await connection.PublishAsync(topic, payload);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
         await connection.PublishAsync($"$az/iot/methods/{cmdName}/response/?rid={rid}&rc={status}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$az/iot/twin/get/?rid={lastRid}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingGetTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback.ReasonCode}' publishing twin GET");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$az/iot/twin/patch/reported/?rid={lastRid}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                pendingUpdateTwinRequests.TryAdd(lastRid++, tcs);
            }
            else
            {
                Trace.TraceError($"Error '{puback.ReasonCode}' publishing twin PATCH");
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        void ConfigureReservedTopics()
        {
           
            var subres = connection.SubscribeAsync(new string[] {
                                                    "$az/iot/methods/+/+",
                                                    "$az/iot/twin/get/response/+",
                                                    "$az/iot/twin/patch/response/+",
                                                    "$az/iot/twin/events/desired-changed/+" }).Result;

            subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

            if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
            {
                throw new ApplicationException("Error subscribing to system topics");
            }


            connection.OnMessage  = async e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string msg = string.Empty;
                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["rid"]);
                    twinVersion = Convert.ToInt32(qs["v"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }

                if (topic.StartsWith("$az/iot/twin/get/response"))
                {   
                    if (pendingGetTwinRequests.TryRemove(rid, out TaskCompletionSource<string> tcs))
                    {
                        tcs.SetResult(msg);
                    }
                }
                else if (topic.StartsWith("$az/iot/twin/patch/response/"))
                {
                    if (pendingUpdateTwinRequests.TryRemove(rid, out TaskCompletionSource<int> tcs))
                    {
                        tcs.SetResult(twinVersion);
                    }
                }
                else if (topic.StartsWith("$az/iot/twin/events/desired-changed"))
                {
                    var ack = await OnPropertyChange?.Invoke(new PropertyReceived()
                    {
                        Rid = rid.ToString(),
                        Topic = e.ApplicationMessage.Topic,
                        PropertyMessageJson = msg,
                        Version = twinVersion
                    });
                    _ = UpdateTwinAsync(ack.BuildAck());
                }
                else if (topic.StartsWith("$az/iot/methods"))
                {
                    var cmdName = segments[3];
                    var resp = await OnCommand?.Invoke(new CommandRequest()
                    {
                        CommandName = cmdName,
                        CommandPayload = msg
                    });
                    _ = CommandResponseAsync(rid.ToString(), cmdName, resp.Status.ToString(), resp.CommandResponsePayload);
                }
                else
                {
                    OnMessage?.Invoke(e);
                }
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    connection?.Dispose();
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

        public Task<MqttClientSubscribeResult> SubscribeAsync(string[] topics)
        {
            return connection.SubscribeAsync(topics);
        }

        public Task<MqttClientPublishResult> PublishAsync(string topic, object payload, CancellationToken cancellationToken)
        {
            return connection.PublishAsync(topic, payload, cancellationToken);
        }

        public Task<MqttClientPublishResult> PublishAsync(string topic, object payload) => PublishAsync(topic, payload, CancellationToken.None);
        
        public Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessage { get; set; }
    }
}