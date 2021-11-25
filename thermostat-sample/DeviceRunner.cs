using com_example;
using Rido.IoTHubClient;

namespace thermostat_sample;

public class DeviceRunner : BackgroundService
{
    const double defaultTargetTemperature = 21;

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
        client = await com_example.thermostat_1.CreateDeviceClientAsync(_configuration.GetConnectionString("cs"), stoppingToken);
        Console.WriteLine(client.ConnectionSettings.ToString());

        client.OnProperty_targetTemperature_Updated = OnProperty_targetTemperatue_Handler;
        client.OnCommand_getMaxMinReport_Invoked = Cmd_getMaxMinReport_Handler;
        
        await client.InitTwinProperty_targetTemperature_Async(defaultTargetTemperature);

        while (!stoppingToken.IsCancellationRequested)
        {
            temperature = Math.Round((temperature % 2) == 0 ? temperature + rndDouble(0.3) : temperature - rndDouble(0.2),2);
            readings.Add(DateTimeOffset.Now, temperature);
            //await client.Send_temperature(temperature);
            Console.Write($"\r-> t: temperature {temperature} \t");
            await Task.Delay(10000, stoppingToken);
        }
    }

    async Task<Cmd_getMaxMinReport_Response> Cmd_getMaxMinReport_Handler(Cmd_getMaxMinReport_Request req)
    {
        ArgumentNullException.ThrowIfNull(client);
        Console.WriteLine("\n<- c: getMaxMinReport " + req.since);

        if (readings.Values.Max<double>() > maxTemp)
        {
            maxTemp = readings.Values.Max<double>();
            await client.Report_maxTempSinceLastReboot(maxTemp);

            Console.WriteLine($"\n-> r: maxTempSinceLastReboot {maxTemp}");
        }

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
    }

    async Task<WritableProperty<double>> OnProperty_targetTemperatue_Handler(WritableProperty<double> prop)
    {
        Console.WriteLine("\n<- w: targetTemperature received " + prop.Value);
        _ = AdjustTempInStepsAsync(prop);
        return await Task.FromResult(new WritableProperty<double>("targetTemperature")
        {
            Version = prop.Version,
            Value = prop.Value,
            Status = 200,
            Description = "Temperature accepted and adjusted to " + prop.Value
        });
    }

    async Task AdjustTempInStepsAsync(WritableProperty<double> prop)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(prop);
        Console.WriteLine("\n adjusting temp to: " + prop.Value);
        
        double step = (prop.Value - temperature) / 5d;
        for (int i = 1; i <= 5; i++)
        {
            await client.UpdateTwinAsync(new WritableProperty<double>("targetTemperature")
            {
                Status = 202,
                Version = prop.Version,
                Value = prop.Value,
                Description = "Step " + i
            }.ToAck());
            
            temperature = Math.Round(temperature + step, 1);
            //await client.Send_temperature(temperature);
            Console.WriteLine($"\r-> t: temperature {temperature} \t");
            readings.Add(DateTimeOffset.Now, temperature);
            await Task.Delay(1000);
        }
        Console.WriteLine("\n temp adjusted to: " + prop.Value);
    }
}
