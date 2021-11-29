using Rido.IoTHubClient;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace sample_device
{
    class ProgramV1
    {
        static async Task Main(string[] args)
        {
            Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);

            
            var client = await HubMqttClient.CreateAsync(ConnectionSettings.FromConnectionString(Environment.GetEnvironmentVariable("cs")));

            client.OnMqttClientDisconnected += (s, e) => Console.WriteLine("Client Disconnected");
           
            Console.WriteLine();
            Console.WriteLine(client.ConnectionSettings);
            Console.WriteLine();

            var twin = await client.GetTwinAsync();
            Console.WriteLine(twin);

            await client.SubscribeAsync(new string[] { "vehicles/#" });
            client.OnMessage = async m =>
            {
                await Task.Delay(100);
                Console.WriteLine(m.ApplicationMessage.Topic);
            };
            await client.PublishAsync("vehicles/vin02/memory", new { Environment.WorkingSet });

            int missedMessages = 0;
            while (missedMessages < 10)
            {
                if (client.IsConnected)
                {
                    await client.SendTelemetryAsync(new { temperature = 21 });
                    Console.Write("t");
                    missedMessages = 0;
                }
                else
                {
                    System.Console.WriteLine("missed messages " + missedMessages);
                    missedMessages++;
                }
                await Task.Delay(2000);
            }
            System.Console.WriteLine("The End");
        }
    }
}
