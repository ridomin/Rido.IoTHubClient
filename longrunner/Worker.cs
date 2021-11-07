using Rido.IoTHubClient;
using MQTTnet.Client;

namespace longrunner;
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    HubMqttConnection? connection;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        connection = await HubMqttConnection.CreateAsync(
            ConnectionSettings.FromConnectionString(
                Environment.GetEnvironmentVariable("dcs")
            )
        );

        _logger.LogInformation(connection.ConnectionSettings.ToString());
        
        await connection.MqttClient.SubscribeAsync("$iothub/methods/POST/#");        
        connection.MqttClient.UseApplicationMessageReceivedHandler(async e =>
        {
            string topic = e.ApplicationMessage.Topic;
            _logger.LogInformation("<-" + topic);
            
            var segments = topic.Split("/");
            var qs = System.Web.HttpUtility.ParseQueryString(segments[^1]);
            var rid = Convert.ToInt32(qs["$rid"]);
            await connection.PublishAsync($"$iothub/methods/res/200/?$rid={rid}", new {resp = "fake"});
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time} status {}", DateTimeOffset.Now, connection.IsConnected );
            await Task.Delay(5000, stoppingToken);
        }
    }
}
