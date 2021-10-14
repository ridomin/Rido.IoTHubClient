using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rido.IoTHubClient;
using System.Diagnostics;
using MQTTnet.Diagnostics;

namespace sample_device
{
    class Program_BYOM
    {

        static string DefaultKey  => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N")));

        public static async Task Main__(string[] args)
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
            MQTTnet.Client.IMqttClient mqttClient = new MqttFactory(logger).CreateMqttClient();

            var connack= await mqttClient.ConnectV2WithSasAsync("broker.azure-devices.net", "d5", DefaultKey, "", 60);

            Console.WriteLine(connack.ResultCode);

            await Task.Delay(1000);

            await mqttClient.DisconnectAsync();
            
            Console.WriteLine(connack.ResultCode);
            Console.WriteLine(connack.MaximumQoS);
            Console.WriteLine(connack.IsSessionPresent);
        }
    }
}
