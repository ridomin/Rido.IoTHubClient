namespace thermostat_sample;

public class DeviceRunner : BackgroundService
{
    static Random random = new();
    static double rndDouble(double scaleFactor = 1.1) => random.NextDouble() * scaleFactor;
    double maxTemp = 0d;
    FixedSizeDictonary<DateTimeOffset, double> readings = new(1000) { { DateTimeOffset.Now, Math.Round(rndDouble(18), 1) } };
    double temperature = Math.Round(rndDouble(18), 1);

    private readonly ILogger<DeviceRunner> _logger;
    private readonly IConfiguration _configuration;

    com_example.thermostat_1? client;

    public DeviceRunner(ILogger<DeviceRunner> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        client = await com_example.thermostat_1.CreateDeviceClientAsync(_configuration.GetConnectionString("cs"));

        client.OnProperty_targetTemperature_Updated = async m =>
        {
            Console.WriteLine("\n<- w: targetTemperature received " + m.Value);
            await AdjustTempInStepsAsync(m.Value);
            await Task.Delay(1000);
            return new Rido.IoTHubClient.WritableProperty<double>("targetTemperature")
            {
                Version = m.Version,
                Value = m.Value,
                Status = 200,
            };
        };

        client.OnCommand_getMaxMinReport_Invoked = async req =>
        {
            Console.WriteLine("\n<- c: getMaxMinReport " + req.since);
            await Task.Delay(100);
            Dictionary<DateTimeOffset, double> filteredReadings = readings
                                           .Where(i => i.Key > req.since)
                                           .ToDictionary(i => i.Key, i => i.Value);
            return new com_example.Cmd_getMaxMinReport_Response
            {
                maxTemp = filteredReadings.Values.Max<double>(),
                minTemp = filteredReadings.Values.Min<double>(),
                avgTemp = filteredReadings.Values.Average(),
                startTime = filteredReadings.Keys.Min(),
                endTime = filteredReadings.Keys.Max(),
                _rid = req._rid,
                _status = 200
            };
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            temperature = Math.Round(
                     (temperature % 2) == 0 ?
                         temperature + rndDouble(0.3) :
                         temperature - rndDouble(0.2),
                     2);

            readings.Add(DateTimeOffset.Now, temperature);

            if (readings.Values.Max<double>() > maxTemp)
            {
                maxTemp = readings.Values.Max<double>();
                await client.Report_maxTempSinceLastReboot(maxTemp);

                Console.WriteLine($"\n-> r: maxTempSinceLastReboot {maxTemp}");
            }

            await client.Send_temperature(temperature);
            Console.Write($"\r-> t: temperature {temperature} \t");
            await Task.Delay(1000, stoppingToken);
        }
    }

    async Task AdjustTempInStepsAsync(double target)
    {
        ArgumentNullException.ThrowIfNull(client);

        Console.WriteLine("\n adjusting temp to: " + target);
        double step = (target - temperature) / 5d;
        for (int i = 1; i <= 5; i++)
        {
            temperature = Math.Round(temperature + step, 1);
            await client.Send_temperature(temperature);
            Console.Write($"\r-> t: temperature {temperature} \t");
            readings.Add(DateTimeOffset.Now, temperature);
            await Task.Delay(1000);
        }
        Console.WriteLine("\n temp adjusted to: " + target);
    }
}
