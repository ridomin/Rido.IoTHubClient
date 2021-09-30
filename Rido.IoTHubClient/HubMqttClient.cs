using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
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
    public class HubMqttClient
    {
        public event EventHandler<CommandEventArgs> OnCommandReceived;
        public event EventHandler<PropertyEventArgs> OnPropertyReceived;
        public event EventHandler<MqttApplicationMessageReceivedEventArgs> OnMessageReceived;

        IMqttClient MqttClient;
        public string ClientId { get; set; }

        // Dictionary<int, Func<string, string>> callbacks = new Dictionary<int, Func<string,string>>();
        static Action<string> cb;
        static Action<int> patch_cb;
        private int lastRid = 1;
        public HubMqttClient(string clientId)
        {
            var factory = new MqttFactory();
            MqttClient = factory.CreateMqttClient();
            ClientId = clientId;
        }

      
        public static async Task<HubMqttClient> CreateWithClientCertAsync(string hostname, string certPath, string certPwd)
        {
            using var cert = new X509Certificate2(certPath, certPwd);
            var cid = cert.Subject.Substring(3);
            List<X509Certificate> certs = new List<X509Certificate> { cert };

            var hub = new HubMqttClient(cid);

            var username = $"av=2021-06-30-preview&h={hostname}&did={cid}&am=X509&dtmi=dtmi:aa:b;1";
            //var username = $"{hostname}.azure-devices.net/{cid}/?api-version=2020-09-30&DeviceClientType=RidoTests&x509=true";
            Console.WriteLine(username);
            Console.WriteLine($"{cert.SubjectName.Name} issued by {cert.IssuerName.Name} NotAfter {cert.GetExpirationDateString()} ({cert.Thumbprint})");

            var options = new MqttClientOptionsBuilder()
                .WithClientId(cid)
                .WithTcpServer(hostname, 8883)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    SslProtocol = SslProtocols.Tls12,
                    Certificates = certs
                })
                .WithCredentials(new MqttClientCredentials()
                {
                    Username = username
                })
                .Build();

            ConfigureReservedTopics(hub);

            await hub.MqttClient.ConnectAsync(options, CancellationToken.None);

            return hub;
        }

        public static async Task<HubMqttClient> CreateFromConnectionStringAsync(string connectionString)
        {
            DateTimeOffset expiry = DateTimeOffset.UtcNow.AddMinutes(60);
            var expiryString = expiry.ToUnixTimeMilliseconds().ToString();

            DeviceConnectionString dcs = new DeviceConnectionString(connectionString);

            var hub = new HubMqttClient(dcs.DeviceId);

            var userName = dcs.GetUserName(expiryString);
            var password = dcs.BuildSasToken(expiryString);
            Console.WriteLine(userName);

            var options = new MqttClientOptionsBuilder()
             .WithClientId(dcs.DeviceId)
             .WithTcpServer(dcs.HostName, 8883)
             .WithCredentials(userName, password)
             .WithTls(new MqttClientOptionsBuilderTlsParameters
             {
                 UseTls = true,
                 SslProtocol = SslProtocols.Tls12
             })
             .WithCleanSession(true)
             .Build();

            ConfigureReservedTopics(hub);
            await hub.MqttClient.ConnectAsync(options, CancellationToken.None);
            return hub;
        }

        private static void ConfigureReservedTopics(HubMqttClient hub)
        {
            hub.MqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string msg = string.Empty;

                var segments = e.ApplicationMessage.Topic.Split('/');
                int rid = 0;
                int twinVersion = 0;
                if (e.ApplicationMessage.Topic.Contains("?"))
                {
                    // parse qs to extract the rid
                    var qs = HttpUtility.ParseQueryString(segments[segments.Length - 1]);
                    rid = Convert.ToInt32(qs["rid"]);
                    twinVersion = Convert.ToInt32(qs["v"]);
                }

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }


                if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin"))
                {
                    Console.WriteLine($"<- {e.ApplicationMessage.Topic}  {e.ApplicationMessage.Payload?.Length} Bytes");
                    if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/get/response"))
                    {
                        cb(msg);
                    }
                    else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/events/desired-changed"))
                    {
                        hub.OnPropertyReceived?.Invoke(hub, new PropertyEventArgs()
                        {
                            Topic = e.ApplicationMessage.Topic,
                            Rid = rid.ToString(),
                            PropertyMessageJson = TwinProperties.RemoveVersion(msg),
                            Version = twinVersion
                        });
                    }
                    else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/twin/patch/response/"))
                    {
                        patch_cb(twinVersion);
                    }
                }
                else if (e.ApplicationMessage.Topic.StartsWith("$az/iot/methods"))
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
                else
                {
                    Console.WriteLine($"<- {e.ApplicationMessage.Topic} {e.ApplicationMessage.Payload?.Length} Bytes");
                    hub.OnMessageReceived?.Invoke(hub, e);
                }
            });

            hub.MqttClient.UseConnectedHandler(async e =>
            {
                Console.WriteLine("### CONNECTED WITH SERVER ###");
                var subres = await hub.MqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                        .WithTopicFilter("$az/iot/methods/+/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/get/response/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/patch/response/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .WithTopicFilter("$az/iot/twin/events/desired-changed/+", MqttQualityOfServiceLevel.AtLeastOnce)
                                                        .Build());
                subres.Items.ToList().ForEach(x => Console.WriteLine($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            });

            hub.MqttClient.UseDisconnectedHandler(e =>
            {
                Console.WriteLine("## DISCONNECT ##");
                Console.WriteLine($"** {e.ClientWasConnected} {e.Reason}");
            });
        }

        public async Task Disconnect()
        {
            await MqttClient.DisconnectAsync();
        }

        public async Task<MqttClientPublishResult> SendTelemetryAsync(object payload) => await PublishAsync("$az/iot/telemetry", payload);

        public async Task<MqttClientPublishResult> RequestTwinAsync(Action<string> GetTwinCallback)
        {
            var puback = await MqttClient.PublishAsync($"$az/iot/twin/get/?rid={lastRid++}");
            //callbacks.Add(lastRid, GetTwinCallback);
            cb = GetTwinCallback;
            if (puback.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                Console.WriteLine(puback.ReasonCode);
            }
            return puback;
        }

        public async Task<MqttClientPublishResult> UpdateTwinAsync(object payload, Action<int> patchTwinCallback)
        {
            var puback = await PublishAsync($"$az/iot/twin/patch/reported/?rid={lastRid++}", payload);
            patch_cb = patchTwinCallback;
            return puback;
        }

        public async Task<MqttClientPublishResult> CommandResponseAsync(string rid, string cmdName, string status, object payload) =>
            await PublishAsync($"$az/iot/methods/{cmdName}/response/?rid={rid}&rc={status}", payload);


        public async Task<MqttClientSubscribeResult> SubscribeAsync(string topic)
        {
            var suback = await MqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic).Build());
            suback.Items.ToList().ForEach(x => Console.WriteLine($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            return suback;
        }

        public async Task<MqttClientPublishResult> PublishAsync(string topic, object payload)
        {
            string jsonPayload;
            if (payload is string)
            {
                jsonPayload = (string)payload;
            }
            else
            {
                jsonPayload = JsonSerializer.Serialize(payload);
            }
            var message = new MqttApplicationMessageBuilder()
                             .WithTopic(topic)
                             .WithPayload(jsonPayload)
                             .Build();

            var pubRes = await MqttClient.PublishAsync(message, CancellationToken.None);
            if (pubRes.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                Console.WriteLine(pubRes.ReasonCode + pubRes.ReasonString);
            }
            Console.WriteLine($"-> {topic} {message.Payload?.Length} Bytes {pubRes.ReasonCode}");
            return pubRes;
        }
    }
}
