using Rido.IoTHubClient;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace sample_device
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);

            //var client = await HubBrokerMqttClient.CreateFromConnectionStringAsync(Environment.GetEnvironmentVariable("cs"));
            var client = await HubBrokerMqttClient.CreateWithClientCertsAsync("broker.azure-devices.net", @"C:\certs\ridocafy22\devx1.pfx", "1234");

            var t = await client.GetTwinAsync();
            Console.WriteLine("Twin REPLY 1" + t);

            var suback = await client.SubscribeAsync("vehicles/#");
            suback.Items.ToList().ForEach(c => Console.WriteLine($"sub to {c.TopicFilter.Topic} {c.ResultCode}"));

            client.OnMessageReceived += (s, e) =>
            {
                string contents = $"{e.ApplicationMessage.Topic} {e.ApplicationMessage.Payload.Length} Bytes";
                Console.WriteLine("Custom Topic Message:" + contents);
            };
            while (true)
            {
                await client.PublishAsync($"vehicles/{client.DeviceConnectionString.DeviceId}/GPS/pos", new { lat = 23.32323, lon = 54.45454 });
                var pubacktel = await client.SendTelemetryAsync(new { temperature = 1 });
                await Task.Delay(5000);
            }
        }
    }
}
