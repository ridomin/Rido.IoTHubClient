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
            var client = await HubMqttClient.CreateWithClientCertsAsync("rido.azure-devices.net","../../../../.certs/devx1.pfx", "1234");
            //var client = await HubMqttClient.CreateFromConnectionStringAsync(Environment.GetEnvironmentVariable("cs"));

            
            client.OnCommandReceived += async (s, e) =>
            {
                Console.WriteLine($"Processing Command {e.CommandName}");
                await Task.Delay(500);
                await client.CommandResponseAsync(e.Rid, e.CommandName, "200", new { myResponse = "ok" });
            };

            client.OnPropertyReceived += async (s, e) => {
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
            
            while (true)
            {
                //await client.PublishAsync($"vehicles/{client.ClientId}/GPS/pos", new { lat = 23.32323, lon = 54.45454 });
                await client.SendTelemetryAsync(new { temperature = 1 });
                await Task.Delay(5000);
            }
        }
    }
}
