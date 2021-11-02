using Rido.IoTHubClient;
using System.Diagnostics;
using System.Text.Json;

Random random = new();
double temperature = 0d;
double maxTemp = 0d;
Dictionary<DateTimeOffset, double> readings = new() { { DateTimeOffset.Now, maxTemp } };

Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);

string connectionString = Environment.GetEnvironmentVariable("cs") ?? throw new ArgumentException("Env Var 'cs' not found.");
Console.WriteLine(connectionString);
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
    Task.Run(async () => await thermostat.Report_targetTemperatureACK(new PropertyAck
    {
        Description = "updating",
        Status = 202,
        Version = m.version,
        Value = JsonSerializer.Serialize(new { temperature })
    }));

    double step = (m.targetTemperature - temperature) / 10d;
    for (int i = 1; i <= 10; i++)
    {
        temperature = Math.Round(temperature + step, 1);
        readings.Add(DateTimeOffset.Now, temperature);


        Task.Delay(1000).Wait();
    }
    return new PropertyAck
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



