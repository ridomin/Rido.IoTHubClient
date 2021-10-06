using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Rido.IoTHubClient
{
    public class CommandEventArgs : EventArgs
    {
        public string Rid { get; set; }
        public string CommandName { get; set; }
        public string CommandRequestMessageJson { get; set; }
        public string Topic { get; set; }
    }

    public class PropertyEventArgs : EventArgs
    {
        public string Rid { get; set; }
        public string PropertyMessageJson { get; set; }
        public string Topic { get; set; }
        public int Version { get; set; }
    }

    public class HubMqttClient
    {
        public event EventHandler<CommandEventArgs> OnCommandReceived;
        public event EventHandler<PropertyEventArgs> OnPropertyReceived;

        private IMqttClient mqttClient;
        public string ClientId { get; set; }

        static Action<string> twin_cb;
        static Action<int> patch_cb;
        private int lastRid = 1;

        HubMqttClient(string clientId)
        {
            mqttClient = new MqttFactory().CreateMqttClient();
            ClientId = clientId;
        }
              
        public static async Task<HubMqttClient> CreateWithClientCertsAsync(string hostname, string certPath, string certPwd)
        {
            using var cert = new X509Certificate2(certPath, certPwd);
            var cid = cert.Subject.Substring(3);
            
            var hub = new HubMqttClient(cid);
            ConfigureReservedTopics(hub);
            
            var username = $"{hostname}/{cid}/?api-version=2020-09-30&DeviceClientType=RidoTests";
            Console.WriteLine($"{cert.SubjectName.Name} issued by {cert.IssuerName.Name} NotAfter {cert.GetExpirationDateString()} ({cert.Thumbprint})");

            await hub.mqttClient.ConnectAsync(
                new MqttClientOptionsBuilder()
                    .WithClientId(cid)
                    .WithTcpServer(hostname, 8883)
                    .WithCredentials(new MqttClientCredentials()
                    {
                        Username = username
                    })
                    .WithTls(new MqttClientOptionsBuilderTlsParameters
                    {
                        UseTls = true,
                        SslProtocol = SslProtocols.Tls12,
                        Certificates = new List<X509Certificate> { cert }
                    })
                    .Build(), 
                CancellationToken.None);
            return hub;
        }

        public static async Task<HubMqttClient> CreateFromConnectionStringAsync(string connectionString) => 
            await CreateFromDCSAsync(new DeviceConnectionString(connectionString));

        public static async Task<HubMqttClient> CreateAsync(string hostName, string deviceId, string sasKey) =>
            await CreateFromDCSAsync(new DeviceConnectionString() { DeviceId = deviceId, HostName = hostName, SharedAccessKey = sasKey });

        private static async Task<HubMqttClient> CreateFromDCSAsync(DeviceConnectionString dcs)
        {
            DateTimeOffset expiry = DateTimeOffset.UtcNow.AddMinutes(60);
            var expiryString = expiry.ToUnixTimeMilliseconds().ToString();

            var hub = new HubMqttClient(dcs.DeviceId);
            ConfigureReservedTopics(hub);
            
            await hub.mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                 .WithClientId(dcs.DeviceId)
                 .WithTcpServer(dcs.HostName, 8883)
                 .WithCredentials(dcs.GetUserName(expiryString), dcs.BuildSasToken(expiryString))
                 .WithTls(new MqttClientOptionsBuilderTlsParameters
                 {
                     UseTls = true,
                     CertificateValidationHandler = (x) => { return true; },
                     SslProtocol = SslProtocols.Tls12
                 })
                 .Build(), 
                 CancellationToken.None);
            return hub;
        }

        static void ConfigureReservedTopics(HubMqttClient hub)
        {

            //MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
            //{
            //    var trace = $">> [{e.TraceMessage.Timestamp:O}] [{e.TraceMessage.ThreadId}] [{e.TraceMessage.Source}] [{e.TraceMessage.Level}]: {e.TraceMessage.Message}";
            //    if (e.TraceMessage.Exception != null)
            //    {
            //        trace += Environment.NewLine + e.TraceMessage.Exception.ToString();
            //    }

            //    Console.WriteLine(trace);
            //};

            hub.mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string msg = string.Empty;

                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (e.ApplicationMessage.Topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[segments.Length - 1]);
                    rid = Convert.ToInt32(qs["$rid"]);
                    twinVersion = Convert.ToInt32(qs["$version"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }

                Console.WriteLine($"<- {e.ApplicationMessage.Topic}  {e.ApplicationMessage.Payload?.Length} Bytes");
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
                    hub.OnPropertyReceived?.Invoke(hub, new PropertyEventArgs()
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Rid = rid.ToString(),
                        PropertyMessageJson = TwinProperties.RemoveVersion(msg),
                        Version = twinVersion
                    });
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/"))
                {
                    var cmdName = segments[3];
                    Console.WriteLine($"<- {e.ApplicationMessage.Topic} {cmdName} {e.ApplicationMessage.Payload.Length} Bytes");
                    hub.OnCommandReceived?.Invoke(hub, new CommandEventArgs()
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Rid = rid.ToString(),
                        CommandName = cmdName,
                        CommandRequestMessageJson = msg
                    });
                }
            });

            hub.mqttClient.UseConnectedHandler(async e =>
            {
                Console.WriteLine("### CONNECTED WITH SERVER ###");
                var subres = await hub.mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                        .WithTopicFilter("$iothub/methods/POST/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .WithTopicFilter("$iothub/twin/res/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .WithTopicFilter("$iothub/twin/PATCH/properties/desired/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                        .Build());
                subres.Items.ToList().ForEach(x => Console.WriteLine($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            });

            hub.mqttClient.UseDisconnectedHandler(e =>
            {
                Console.WriteLine("## DISCONNECT ##");
                Console.WriteLine($"** {e.ClientWasConnected} {e.Reason}");
            });
        }

        public async Task SendTelemetryAsync(object payload) => await PublishAsync($"devices/{this.ClientId}/messages/events/", payload);
        
        public async Task CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
          await PublishAsync($"$iothub/methods/res/{status}/?$rid={rid}", payload);

        public async Task<string> GetTwinAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            var puback = await PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                twin_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                twin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin GET: {s}"));
            }
            return tcs.Task.Result;
        }

        public async Task<int> UpdateTwinAsync(object payload)
        {
            var tcs = new TaskCompletionSource<int>();
            var puback = await PublishAsync($"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", payload);
            if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                patch_cb = s => tcs.TrySetResult(s);
            }
            else
            {
                patch_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
            }
            return tcs.Task.Result;
        }

        async Task<MqttClientPublishResult> PublishAsync(string topic, object payload)
        {
            string jsonPayload;
            if (payload is string)
            {
                jsonPayload = payload as string;
            }
            else
            {
                jsonPayload = JsonSerializer.Serialize(payload);
            }
            var message = new MqttApplicationMessageBuilder()
                              .WithTopic(topic)
                              .WithPayload(jsonPayload)
                              .Build();

            var pubRes = await mqttClient.PublishAsync(message, CancellationToken.None);
            if (pubRes.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                Console.WriteLine(pubRes.ReasonCode + pubRes.ReasonString);
            }
            Console.WriteLine($"-> {topic} {message.Payload?.Length} Bytes {pubRes.ReasonCode}");
            return pubRes;
        }
    }
}
