using System.Text.Json;

Random random = new();
double temperature = 0d;
double maxTemp = 0d;
Dictionary<DateTimeOffset, double> readings = new();

string connectionString = Environment.GetEnvironmentVariable("cs") ?? throw new ArgumentException("Env Var 'cs' not found.");

Thermostat thermostat = new(connectionString);

await thermostat.Report_maxTempSinceLastReboot(maxTemp);
Console.WriteLine("-> r: maxTempSinceLastReboot " + maxTemp);

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

thermostat.OntargetTemperatureUpdated = m =>
{
    Console.WriteLine("<- w: targetTemperature received " + m.targetTemperature);
    //await thermostat.Ack_TargetTemperature(temperature, 202, m.version);
    double step = (m.targetTemperature - temperature) / 5d;
    for (int i = 1; i <= 2; i++)
    {
        temperature = Math.Round(temperature + step, 1);
        Task.Delay(6 * 1000);
    }
    return new Rido.IoTHubClient.PropertyAck()
    {
        Status = 200,
        Value = JsonSerializer.Serialize(new { temperature }),
        Version = m.version
    };
};

while (true)
{
    temperature = Math.Round(random.NextDouble() * 40.0 + 5.0, 1);
    readings.Add(DateTimeOffset.Now, temperature);

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



