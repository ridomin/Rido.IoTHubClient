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

        Timer refreshScreenTimer;
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
                HostName = "rido-freetier.azure-devices.net",
                DeviceId = "longrunner",
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

            

            refreshScreenTimer = new Timer((tcb) => 
            {
                refreshScreen(); 
            }, null, 1000, 0);

            while (!stoppingToken.IsCancellationRequested)
            {
                var puback = await client.SendTelemetryAsync(new {Environment.WorkingSet});
                if (puback == 0) SendTelemetrySuccess++;
                await Task.Delay(5000);
            }
           
        }

        void refreshScreen()
            {
                Console.Clear();
                Console.WriteLine(RenderData());
                refreshScreenTimer = new Timer((tcb) => 
                {
                    refreshScreen(); 
                }, null, 1000, 0);
            
            }
        
        string RenderData()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("mqtt runner");
            sb.AppendLine("");
            sb.AppendLine(client.ConnectionSettings.ToString());
            sb.AppendLine("");
            sb.AppendLine($"{nameof(Commands)}: {Commands}");
            sb.AppendLine($"{nameof(Disconnects)}: {Disconnects}");
            //sb.AppendLine("");
            //sb.AppendLine("config: " + configuration.GetSection("SimParams").GetValue<int>("UnNumero"));
            sb.AppendLine($"{nameof(SendTelemetrySuccess)}: {SendTelemetrySuccess}");
            sb.AppendLine("");
            sb.AppendLine($"WorkingSet: {Environment.WorkingSet.Bytes()}");
            sb.Append($"Time Running: {TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds).Humanize(2)}");
            sb.AppendLine("");
            return sb.ToString();
        }
    }