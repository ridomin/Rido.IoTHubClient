﻿using Rido.IoTHubClient;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace sample_device
{
    class ProgramV1
    {
        static async Task Main(string[] args)
        {
            //var dpsRes = await DpsClient.ProvisionWithSasAsync("0ne00385995", "paad", "lD9e/S1YjubD2yRUdkzUI/uPME6KP4Es4Ulhh2Kyh1g=");
            //Console.WriteLine(dpsRes.registrationState.assignedHub);
            //var client1 = await HubMqttClient.CreateAsync(dpsRes.registrationState.assignedHub, dpsRes.registrationState.deviceId, "lD9e/S1YjubD2yRUdkzUI/uPME6KP4Es4Ulhh2Kyh1g=");
            //var t1 = await client1.GetTwinAsync();
            //Console.WriteLine("Twin1 REPLY 1" + t1);

            Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);

            //string modelId = "dtmi:com:demos;1";
            //var client = await HubMqttClient.CreateWithClientCertsAsync("rido.azure-devices.net","../../../../.certs/devx1.pfx", "1234", modelId);
            var client = await HubMqttClient.CreateFromConnectionStringAsync(Environment.GetEnvironmentVariable("cs"));

            Console.WriteLine();
            Console.WriteLine(client.ConnectionSettings);
            Console.WriteLine();

            var t = await client.GetTwinAsync();
            Console.WriteLine("Twin REPLY 1" + t);

            client.OnMqttClientDisconnected += (s, e) =>
            {
                Console.WriteLine("Client Disconnected");
            };

            client.OnCommand = async req => 
            {
                System.Console.WriteLine($"<- Received Command {req.CommandName}");
                await Task.Delay(100);
                string payload = req.CommandPayload;
                System.Console.WriteLine(payload);
                return new CommandResponse
                {
                    _status = 200,
                    CommandResponsePayload = new { myResponse = "all good"}
                };
            };

            client.OnPropertyChange = async e =>
            {
                System.Console.WriteLine($"<- Received property {e.PropertyMessageJson}");
                await Task.Delay(100);
                return new PropertyAck()
                {
                    Version = e.Version,
                    Status = 200,
                    Description = "testing acks",
                    Value = e.PropertyMessageJson
                };
            };
            
            await Task.Delay(500);
            await client.SendTelemetryAsync(new { temperature = 1 });


            var v = await client.UpdateTwinAsync(new { tool = "from mqttnet22 " + System.Environment.TickCount });
            Console.WriteLine("Twin PATCHED version: " + v);

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
