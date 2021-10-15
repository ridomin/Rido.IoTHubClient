using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{

    public class DpsClient
    {
        static IMqttClient _mqttClient;
        static int rid = 1;
        static DpsClient()
        {
            MqttNetLogger logger = new MqttNetLogger();
            logger.LogMessagePublished += (s, e) =>
            {
                var trace = $">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}]: {e.LogMessage.Message}";
                if (e.LogMessage.Exception != null)
                {
                    trace += Environment.NewLine + e.LogMessage.Exception.ToString();
                }

                Trace.TraceInformation(trace);
            };
            var factory = new MqttFactory(logger);
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
            suback.Items.ToList().ForEach(x => Trace.TraceWarning($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
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
            var password = SasAuth.CreateSasToken(resource, sasKey, 5);

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
            suback.Items.ToList().ForEach(x => Trace.TraceWarning($"+ {x.TopicFilter.Topic} {x.ResultCode}"));
            await ConfigureDPSFlowAsync(registrationId, tcs);
            return tcs.Task.Result;

        }

        private static async Task ConfigureDPSFlowAsync(string registrationId, TaskCompletionSource<DpsStatus> tcs)
        {
            string msg = string.Empty;
            _mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                var topic = e.ApplicationMessage.Topic;

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
                        await Task.Delay(900);
                        var pollTopic = $"$dps/registrations/GET/iotdps-get-operationstatus/?$rid={rid}&operationId={dpsRes.operationId}";
                        var puback = await _mqttClient.PublishAsync(pollTopic);
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
        }
    }
}
