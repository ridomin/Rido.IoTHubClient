using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Humanizer;
using Rido.IoTHubClient;

namespace mqtt_runner;

public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        Stopwatch clock = Stopwatch.StartNew();
        IConfiguration configuration;
        IHubMqttClient client;

        int SendTelemetrySuccess = 0;
        int Disconnects = 0;
        int Commands = 0;
        public Worker(ILogger<Worker> log, IConfiguration config)
        {
            _logger = log;
            configuration = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            client = await HubMqttClient.CreateAsync(new ConnectionSettings()
            {
                HostName = "tests.azure-devices.net",
                DeviceId = "d4",
                SharedAccessKey = System.Convert.ToBase64String(
                                        System.Text.Encoding.UTF8.GetBytes(
                                            System.Guid.Empty.ToString("N"))),
                SasMinutes = 3
            });
            
            client.OnMqttClientDisconnected += (o,e) => Disconnects++;
            client.OnCommand = async c => {
                Commands++;
                return await Task.FromResult(
                    new CommandResponse() 
                        { Status =200, CommandResponsePayload = "{}"});
            }; 

            while (!stoppingToken.IsCancellationRequested)
            {
                var puback = await client.SendTelemetryAsync(new {Environment.WorkingSet});
                if (puback == 0) SendTelemetrySuccess++;
                Console.Clear();
                Console.Write(RenderData());
                await Task.Delay(1000);
                Console.CursorVisible = false;
            }
           
        }
        
        string RenderData()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("mqtt runner");
            sb.AppendLine("");
            sb.AppendLine(client.ConnectionSettings.ToString());
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine($"{nameof(Commands)}: {Commands}");
            sb.AppendLine($"{nameof(Disconnects)}: {Disconnects}");
            //sb.AppendLine("");
            //sb.AppendLine("config: " + configuration.GetSection("SimParams").GetValue<int>("UnNumero"));
            sb.AppendLine($"{nameof(SendTelemetrySuccess)}: {SendTelemetrySuccess}");
            sb.AppendLine("");
            sb.AppendLine($"WorkingSet: {Environment.WorkingSet.Bytes()}");
            sb.Append($"Time Running: {TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds).Humanize(4)}");
            return sb.ToString();
        }
    }