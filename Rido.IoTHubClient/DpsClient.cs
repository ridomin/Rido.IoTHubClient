using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{

    public class RegistrationState
    {
        public string registrationId { get; set; }
        public  string assignedHub { get; set; }
        public string deviceId { get; set; }
        public string subStatus { get; set; }
    }

    public class DpsStatus
    {
        public string operationId { get; set; }
        public string status { get; set; }
        public RegistrationState registrationState { get; set; }
    }

    public class DpsClient
    {
        static IMqttClient _mqttClient;
        static int rid = 1;
        static DpsClient()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
        }

        public static async Task<DpsStatus> ProvisionWithCertAsync(string idScope, string pfxPath, string pfxPwd)
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            var tcs = new TaskCompletionSource<DpsStatus>();

            X509Certificate2 cert = new X509Certificate2(pfxPath, pfxPwd);
            var registrationId = cert.SubjectName.Name.Substring(3);
            var resource = $"{idScope}/registrations/{registrationId}";
            var username = $"{resource}/api-version=2019-03-31";

            var options = new MqttClientOptionsBuilder()
                .WithClientId(registrationId)
                .WithTcpServer("global.azure-devices-provisioning.net", 8883)
                .WithCredentials(new MqttClientCredentials()
                {
                    Username = username
                })
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    CertificateValidationHandler = (x) => { return true; },
                    SslProtocol = SslProtocols.Tls12,
                    Certificates = new List<X509Certificate> { cert }
                })
                .Build();

            await _mqttClient.ConnectAsync(options);

            var suback = await _mqttClient.SubscribeAsync("$dps/registrations/res/#");
            suback.Items.ToList().ForEach(x => Console.WriteLine($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            await ConfigureDPSFlowAsync(registrationId, tcs);
            return tcs.Task.Result;
        }

        public static async Task<DpsStatus> ProvisionWithSasAsync(string idScope, string registrationId, string sasKey)
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            var tcs = new TaskCompletionSource<DpsStatus>();

            var resource = $"{idScope}/registrations/{registrationId}";
            var username = $"{resource}/api-version=2019-03-31";
            var password = CreateSasToken(resource, sasKey, TimeSpan.FromMinutes(5));

            var options = new MqttClientOptionsBuilder()
                .WithClientId(registrationId)
                .WithTcpServer("global.azure-devices-provisioning.net", 8883)
                .WithCredentials(username, password)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    CertificateValidationHandler = (x) => { return true; },
                    SslProtocol = SslProtocols.Tls12
                })
            .Build();

            await _mqttClient.ConnectAsync(options);

            var suback = await _mqttClient.SubscribeAsync("$dps/registrations/res/#");
            suback.Items.ToList().ForEach(x => Console.WriteLine($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            await ConfigureDPSFlowAsync(registrationId, tcs);
            return tcs.Task.Result;

        }

        private static async Task ConfigureDPSFlowAsync(string registrationId, TaskCompletionSource<DpsStatus> tcs)
        {
            string msg = string.Empty;
            _mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                Console.WriteLine($"<-{topic}");

                if (e.ApplicationMessage.Payload != null)
                {
                    msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                }

                if (e.ApplicationMessage.Topic.StartsWith($"$dps/registrations/res/"))
                {
                    var topicSegments = topic.Split('/');
                    int reqStatus = Convert.ToInt32(topicSegments[3]);
                    if (reqStatus >= 400)
                    {
                        tcs.SetException(new ApplicationException(msg));
                    }
                    var dpsRes = JsonSerializer.Deserialize<DpsStatus>(msg);
                    if (dpsRes.status == "assigning")
                    {
                        await Task.Delay(333);
                        var pollTopic = $"$dps/registrations/GET/iotdps-get-operationstatus/?$rid={rid}&operationId={dpsRes.operationId}";
                        var puback = await _mqttClient.PublishAsync(pollTopic);
                        Console.WriteLine($"-> {pollTopic} {puback.ReasonCode}");

                    }
                    else
                    {
                        tcs.TrySetResult(dpsRes);
                        rid++;
                    }
                }
            });

            var putTopic = $"$dps/registrations/PUT/iotdps-register/?$rid={rid}";
            var puback = await _mqttClient.PublishAsync(putTopic,
                "{ \"registrationId\" : \"" + registrationId + "\"}");
            Console.WriteLine($"-> {putTopic} {puback.ReasonCode}");
        }

        static string BuildExpiresOn(TimeSpan timeToLive)
        {
            DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime expiresOn = DateTime.UtcNow.Add(timeToLive);
            TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
            long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
            return Convert.ToString(seconds, CultureInfo.InvariantCulture);
        }

        static string CreateSasToken(string resource, string sasKey, TimeSpan ttl)
        {
            var expiry = BuildExpiresOn(ttl);
            var sig = WebUtility.UrlEncode(Sign($"{resource}\n{expiry}", sasKey));
            return $"SharedAccessSignature sr={resource}&sig={sig}&se={expiry}"; // &skn=registration";
        }

        static string Sign(string requestString, string key)
        {
            using var algorithm = new HMACSHA256(Convert.FromBase64String(key));
            return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
        }

       
    }
}
