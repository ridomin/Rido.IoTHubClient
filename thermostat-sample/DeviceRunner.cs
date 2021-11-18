namespace thermostat_sample;

public class DeviceRunner : BackgroundService
{
    private readonly ILogger<DeviceRunner> _logger;
    private readonly IConfiguration _configuration;

    public DeviceRunner(ILogger<DeviceRunner> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
