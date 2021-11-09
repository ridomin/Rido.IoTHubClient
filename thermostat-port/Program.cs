string js(object o) => JsonSerializer.Serialize(o);

Random random = new();
double rndDouble(double scaleFactor = 1.0) => random.NextDouble() * scaleFactor;

double temperature = Math.Round(rndDouble(18), 1);
double maxTemp = 0d;
Dictionary<DateTimeOffset, double> readings = new() { { DateTimeOffset.Now, temperature } };

string connectionString = Environment.GetEnvironmentVariable("cs") ?? throw new ArgumentException("Env Var 'cs' not found.");
Thermostat thermostat = await Thermostat.CreateAsync(connectionString);
Console.WriteLine(thermostat.connection.ConnectionSettings);

thermostat.Command_getMaxMinReport = req =>
{
    Console.WriteLine("\n<- c: getMaxMinReport " + req.since);
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

thermostat.OntargetTemperatureUpdated = async m =>
{
    Console.WriteLine("\n<- w: targetTemperature received " + m.targetTemperature);
    await thermostat.Report_targetTemperatureACK(new PropertyAck
    {
        Description = "updating",
        Status = 202,
        Version = m.version,
        Value = js(new { targetTemperature = temperature })
    });

    await AdjustTempInStepsAsync(m.targetTemperature);

    return new PropertyAck()
    {
        Description = "updated",
        Status = 200,
        Version = m.version,
        Value = js(new { targetTemperature = temperature })
    };
};

var targetTemperature = await thermostat.GetTargetTemperature();
if (targetTemperature?.targetTemperature != null)
{
    await AdjustTempInStepsAsync(targetTemperature.targetTemperature);
}

while (true)
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
        await thermostat.Report_maxTempSinceLastReboot(maxTemp);

        Console.WriteLine($"\n-> r: maxTempSinceLastReboot {maxTemp}");
    }

    await thermostat.Send_temperature(temperature);
    Console.Write($"\r-> t: temperature {temperature} \t");

    await Task.Delay(10000);
}

async Task AdjustTempInStepsAsync(double target)
{
    Console.WriteLine("\n adjusting temp to: " + target);
    double step = (target - temperature) / 5d;
    for (int i = 1; i <= 5; i++)
    {
        temperature = Math.Round(temperature + step, 1);
        await thermostat.Send_temperature(temperature);
        Console.Write($"\r-> t: temperature {temperature} \t");
        readings.Add(DateTimeOffset.Now, temperature);
        await Task.Delay(1000);
    }
    Console.WriteLine("\n temp adjusted to: " + target);
}