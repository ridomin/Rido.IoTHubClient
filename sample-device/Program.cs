using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using Rido.IoTHubClient;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace sample_device
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var mqttClient = new MqttFactory().CreateMqttClient(); //CreateMqttClientWithDiagnostics();  
            var dcs = new DeviceConnectionString(Environment.GetEnvironmentVariable("cs"));
            System.Console.WriteLine(dcs);
            var connack = await mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey);
            Console.WriteLine($"{nameof(mqttClient.IsConnected)}:{mqttClient.IsConnected} . {connack.ResultCode}");

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                Console.Write($"<- {e.ApplicationMessage.Topic} {e.ApplicationMessage.Payload.Length} Bytes: ");
                Console.WriteLine(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
                //Console.Write('.');
            });

            var topic = $"vehicles";
            var subAck = await mqttClient.SubscribeAsync(topic + $"/{dcs.DeviceId}/nodes/+/telemetry");
            subAck.Items.ForEach(x => Console.WriteLine($"{x.TopicFilter}{x.ResultCode}"));

            while (true)
            {
                string pubtopic = $"{topic}/{dcs.DeviceId}/nodes/{new Random().Next(100)}/telemetry";
                var msg = Environment.TickCount64.ToString();
                var pubAck = await mqttClient.PublishAsync(pubtopic, msg);
                Console.WriteLine($"-> {pubtopic} {msg}. {pubAck.ReasonCode}");
                await Task.Delay(1000);
            }

            //ConfigTracing();
            //await DPSProvisionAndConnect();
            //var client = await HubMqttClient.CreateFromConnectionStringAsync(Environment.GetEnvironmentVariable("cs"));
            //await RunAppWithReservedTopics(client);


        }

        private static IMqttClient CreateMqttClientWithDiagnostics()
        {
            Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Information);
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

            return  new MqttFactory(logger).CreateMqttClient();
        }

        static async Task RunAppWithReservedTopics(IHubMqttClient client)
        {
            Console.WriteLine();
            Console.WriteLine(client.DeviceConnectionString);
            Console.WriteLine();

            client.OnMqttClientDisconnected += (s, e) =>
            {
                Console.WriteLine("Client Disconnected");
            };

            client.OnCommandReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Command {e.CommandName}");
                await Task.Delay(500);
                await client.CommandResponseAsync(e.Rid, e.CommandName, "200", new { myResponse = "ok" });
            };

            client.OnPropertyReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                await Task.Delay(500);
                // todo parse property
                var ack = TwinProperties.BuildAck(e.PropertyMessageJson, e.Version, 200, "update ok");
                var v = await client.UpdateTwinAsync(ack);
                Console.WriteLine("PATCHED ACK: " + v);
            };

            await Task.Delay(500);
            await client.SendTelemetryAsync(new { temperature = 1 });

            var t = await client.GetTwinAsync();
            Console.WriteLine("Twin REPLY 1" + t);

            var v = await client.UpdateTwinAsync(new { tool = "from mqttnet22 " + System.Environment.TickCount });
            Console.WriteLine("Twin PATCHED version: " + v);
            int counter = 0;
            while (true)
            {
                await client.SendTelemetryAsync(new { temperature = counter++ });
                await Task.Delay(2000);
                Console.Write("t");
            }
        }

       static  async Task DPSProvisionAndConnect()
        {
            var dpsRes = await DpsClient.ProvisionWithSasAsync("0ne003861C6", "sampleDevice", "Ne+NEEj/NNYkbHGHx0NRoJwZHmN3LoFve2tAdwnCDFQ=");
            Console.WriteLine(dpsRes.registrationState.assignedHub);
            var client = await HubMqttClient.CreateAsync(dpsRes.registrationState.assignedHub, dpsRes.registrationState.deviceId, "lD9e/S1YjubD2yRUdkzUI/uPME6KP4Es4Ulhh2Kyh1g=");

        }
    }
}
