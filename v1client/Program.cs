using Microsoft.Azure.Devices.Client;
using System;
using System.Threading.Tasks;

namespace v1client
{
    class Program
    {
        private static readonly ConsoleEventListener _listener = new ConsoleEventListener();

        static Program()
        {

        }
        static async Task Main(string[] args)
        {
            var cs = Environment.GetEnvironmentVariable("CS");
            var dc = DeviceClient.CreateFromConnectionString(cs, TransportType.Mqtt);
            var twin = await dc.GetTwinAsync();
            Console.WriteLine(twin.ToJson(Newtonsoft.Json.Formatting.Indented));

            dc.SetConnectionStatusChangesHandler((s, r) =>
            {
                Console.WriteLine(s + r.ToString());
            });

            await dc.SetDesiredPropertyUpdateCallbackAsync((a, b) => 
            {
                Console.WriteLine(a.ToJson(Newtonsoft.Json.Formatting.Indented));
                return Task.FromResult(0);
            }, null);

            Console.ReadLine();
        }
    }
}
