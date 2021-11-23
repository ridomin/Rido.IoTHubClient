﻿using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient
{
    public class HubMqttClient : IHubMqttClient, IDisposable
    {
        public ConnectionSettings ConnectionSettings => connection.ConnectionSettings;
        public bool IsConnected => connection.IsConnected;

        public event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        public Func<CommandRequest, Task<CommandResponse>> OnCommand { get; set; }
        public Func<PropertyReceived, Task<PropertyAck>> OnPropertyChange { get; set; }

        const int twinOperationTimeoutSeconds = 5;

        static Action<string> twin_cb;
        static Action<int> patch_cb;

        int lastRid = 1;
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
        }

        public async Task CloseAsync()
        {
            await connection.CloseAsync();
        }

        public async Task<PubResult> SendTelemetryAsync(object payload, string dtdlComponentname = "")
        {
            string topic = $"devices/{connection.ConnectionSettings.DeviceId}";

            if (!string.IsNullOrEmpty(connection.ConnectionSettings.ModuleId))
            {
                topic += $"/modules/{connection.ConnectionSettings.ModuleId}";
            }
            topic += "/messages/events/";

            if (!string.IsNullOrEmpty(dtdlComponentname))
            {
                topic += $"$.sub={dtdlComponentname}";
            }
            var pubAck = await connection.PublishAsync(topic, payload);
            var pubResult = (PubResult)pubAck.ReasonCode;
            return pubResult;
        }

        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await connection.PublishAsync($"$iothub/methods/res/{status}/?$rid={rid}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                twin_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                twin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin GET: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var puback = await connection.PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                patch_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                patch_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
            }
            return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(twinOperationTimeoutSeconds));
        }

        void ConfigureReservedTopics()
        {
            Trace.TraceWarning("### CONNECTED WITH SERVER ###");
            var subres = connection.SubscribeAsync(new string[] {
                                                    "$iothub/methods/POST/#",
                                                    "$iothub/twin/res/#",
                                                    "$iothub/twin/PATCH/properties/desired/#" }).Result;
            subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

            if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
            {
                throw new ApplicationException("Error subscribing to system topics");
            }


            connection.OnMessage  = async e =>
            {
                string msg = string.Empty;

                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (e.ApplicationMessage.Topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[^1]);
                    rid = Convert.ToInt32(qs["$rid"]);
                    twinVersion = Convert.ToInt32(qs["$version"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }

                //Trace.TraceWarning($"<- {e.ApplicationMessage.Topic}  {e.ApplicationMessage.Payload?.Length} Bytes");
                if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/200"))
                {   
                    twin_cb(msg);
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/204"))
                {
                    patch_cb(twinVersion);
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
                {
                    var ack = await OnPropertyChange?.Invoke(new PropertyReceived()
                    {
                        Rid = rid.ToString(),
                        Topic = e.ApplicationMessage.Topic,
                        PropertyMessageJson = msg,
                        Version = twinVersion
                    });
                    await UpdateTwinAsync(ack.BuildAck());
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/"))
                {
                    var cmdName = segments[3];
                    var resp = await OnCommand?.Invoke(new CommandRequest()
                    {
                        CommandName = cmdName,
                        CommandPayload = msg
                    });
                    await CommandResponseAsync(rid.ToString(), cmdName, resp.Status.ToString(), resp.CommandResponsePayload);
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
            throw new NotImplementedException("Pub/Sub not enabled in classic hubs");
        }

        public Task<MqttClientPublishResult> PublishAsync(string topic, object payload, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Pub/Sub not enabled in classic hubs");
        }

        public Task<MqttClientPublishResult> PublishAsync(string topic, object payload)
        {
            throw new NotImplementedException("Pub/Sub not enabled in classic hubs");
        }
        public Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessage { get; set; }
    }
}
