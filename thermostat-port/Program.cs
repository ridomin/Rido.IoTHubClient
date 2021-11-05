using System.Collections.Concurrent;

Random random = new();
double temperature = 0d;
double maxTemp = 0d;
ConcurrentDictionary<DateTimeOffset, double> readings = new();
readings.TryAdd(DateTimeOffset.Now, 0);

string connectionString = Environment.GetEnvironmentVariable("central") ?? throw new ArgumentException("Env Var 'cs' not found.");
Thermostat thermostat = await Thermostat.CreateAsync(connectionString);
Console.WriteLine(thermostat.connection.ConnectionSettings);

var targetTemperature = await thermostat.GetTargetTemperature();
AdjustTempInSteps(targetTemperature.targetTemperature);


thermostat.Command_getMaxMinReport = req =>
{
    Console.WriteLine("<- c: getMaxMinReport " + req.since);
    Dictionary<DateTimeOffset, double> filteredReadings = readings
                                           .Where(i => i.Key > req.since)
                                           .ToDictionary(i => i.Key, i => i.Value);
    return new Command_getMaxMinReport_Response
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

thermostat.OntargetTemperatureUpdated = async  m =>
{
    Console.WriteLine("<- w: targetTemperature received " + m.targetTemperature);
    await thermostat.Report_targetTemperatureACK(new PropertyAck
    {
        Description = "updating",
        Status = 202,
        Version = m.version,
        Value = JsonSerializer.Serialize(new { temperature })
    });

    AdjustTempInSteps(m.targetTemperature);

    return new PropertyAck()
    {
        Description = "updated",
        Status = 200,
        Version = m.version,
        Value = JsonSerializer.Serialize(new { temperature })
    };
};

while (true)
{
    if (readings.Values.Max<double>() > maxTemp)
    {
        maxTemp = readings.Values.Max<double>();
        await thermostat.Report_maxTempSinceLastReboot(maxTemp);
        Console.WriteLine("-> r: maxTempSinceLastReboot " + maxTemp);
    }
    await thermostat.Send_temperature(temperature);
    Console.WriteLine("-> t: temperature " + temperature);
    await Task.Delay(2000);
}

void AdjustTempInSteps(double target)
{
    Console.WriteLine("adjusting temp to: " + target);
    Task.Run(async () =>
    {
        double step = (target - temperature) / 10d;
        for (int i = 1; i <= 10; i++)
        {
            temperature = Math.Round(temperature + step, 1);
            readings.TryAdd(DateTimeOffset.Now, temperature);
            await Task.Delay(1000);
        }
    });
}

