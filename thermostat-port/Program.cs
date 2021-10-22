Random random = new();
double temperature = 0d;
double maxTemp = 0d;
Dictionary<DateTimeOffset, double> readings = new();

string connectionString = Environment.GetEnvironmentVariable("cs") ?? throw new ArgumentException("Env Var 'cs' not found.");

Thermostat thermostat = new(connectionString);

await thermostat.Report_maxTempSinceLastReboot(maxTemp);
Console.WriteLine("-> r: maxTempSinceLastReboot " + maxTemp);

thermostat.OntargetTemperatureUpdated += (o, m) =>
{
    Console.WriteLine("<- w: targetTemperature received " + m.targetTemperature);
    double targetTemp = m.targetTemperature;
};

thermostat.OngetMaxMinReportCalled += (o, m) =>
{
    Console.WriteLine("<- c: getMaxMinReport " + m.since);
    Dictionary<DateTimeOffset, double> filteredReadings = readings
                                           .Where(i => i.Key > m.since)
                                           .ToDictionary(i => i.Key, i => i.Value);
    var report = new
    {
        maxTemp = filteredReadings.Values.Max<double>(),
        minTemp = filteredReadings.Values.Min<double>(),
        avgTemp = filteredReadings.Values.Average(),
        startTime = filteredReadings.Keys.Min(),
        endTime = filteredReadings.Keys.Max(),
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



