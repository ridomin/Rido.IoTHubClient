using Rido.IoTHubClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sample_device
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            //var client = await HubMqttClient.CreateWithClientCertsAsync(
            //                     "broker.azure-devices.net",
            //                     "../../../../.certs/device01.pfx", "1234");

            var cs = Environment.GetEnvironmentVariable("cs");
            var client = await HubMqttClient.CreateFromConnectionStringAsync(cs);

            client.OnMessageReceived += (s, e) =>
            {
                string payload = (e.ApplicationMessage.Topic);
            };

            await client.SubscribeAsync("vehicles/#");
            await client.PublishAsync($"vehicles/{client.ClientId}/GPS/pos",
                                     new { lat = 23.32323, lon = 54.45454 });

            client.OnCommandReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Command {e.CommandName}");
                await Task.Delay(500);
                await client.CommandResponseAsync(e.Rid, e.CommandName, new { myResponse = "ok" }, "200");
            };

            client.OnPropertyReceived += async (s, e) => {
                Console.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
                await Task.Delay(500);
                // todo parse property
                await client.UpdateTwinAsync(new { tool = new { ac = 200, av = e.Version, ad = "updated", value = "put value here" } }, v => Console.WriteLine("PATCHED ACK: " + v));
            };

            await client.RequestTwinAsync(s => Console.WriteLine("Twin REPLY 1" + s));
            await client.UpdateTwinAsync(new { tool = "from mqttnet " + System.Environment.TickCount }, v => Console.WriteLine("Twin PATCHED version: " + v));
            await Task.Delay(500);
            await client.RequestTwinAsync(s => Console.WriteLine("Twin REPLY 2" + s));

            while (true)
            {
                //await client.PublishAsync($"vehicles/{client.ClientId}/GPS/pos", new { lat = 23.32323, lon = 54.45454 });
                await client.SendTelemetryAsync(new { temperature = 1 });
                await Task.Delay(5000);
            }
        }
    }
}
