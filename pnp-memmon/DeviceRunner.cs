using System.Text;
using System.Diagnostics;
using Rido.IoTHubClient;
using dtmi_rido_pnp;
using Humanizer;

namespace pnp_memmon;

public class DeviceRunner : BackgroundService
{
    private readonly ILogger<DeviceRunner> _logger;
    private readonly IConfiguration _configuration;
    private Timer? screenRefresher;
    readonly Stopwatch clock = Stopwatch.StartNew();

    double telemetryWorkingSet = 0;

    int telemetryCounter = 0;
    int commandCounter = 0;
    int twinCounter = 0;
    int reconnectCounter = 0;

    dtmi_rido_pnp.memmon? client;

    const bool default_enabled = true;
    const int default_interval = 8;

    public DeviceRunner(ILogger<DeviceRunner> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("Connecting..");
        client = await dtmi_rido_pnp.memmon.CreateDeviceClientAsync(_configuration.GetConnectionString("hub"), stoppingToken) ??
            throw new ApplicationException("Error creating MQTT Client");

        client._connection.OnMqttClientDisconnected += (o, e) => reconnectCounter++;

        client.OnProperty_enabled_Updated = Property_enabled_UpdateHandler;
        client.OnProperty_interval_Updated = Property_interval_UpdateHandler;
        client.OnCommand_getRuntimeStats_Invoked = Command_getRuntimeStats_Handler;

        _ = await client.Report_started_Async(DateTime.Now);

        await client.InitProperty_enabled_Async(default_enabled);
        await client.InitProperty_interval_Async(default_interval);

        screenRefresher = new Timer(RefreshScreen, this, 1000, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (client?.Property_enabled?.Value == true)
            {
                telemetryWorkingSet = Environment.WorkingSet;
                await client.Send_workingSet_Async(telemetryWorkingSet, stoppingToken);
                telemetryCounter++;
            }
            var interval = client?.Property_interval?.Value;
            await Task.Delay(interval.HasValue ? interval.Value * 1000 : 1000, stoppingToken);
        }
    }

    async Task<WritableProperty<bool>> Property_enabled_UpdateHandler(WritableProperty<bool> req)
    {
        twinCounter++;
        var ack = new WritableProperty<bool>("enabled")
        {
            Description = "desired notification accepted",
            Status = 200,
            Version = req.Version,
            Value = req.Value
        };
        return await Task.FromResult(ack);
    }

    async Task<WritableProperty<int>> Property_interval_UpdateHandler(WritableProperty<int> req)
    {
        ArgumentNullException.ThrowIfNull(client);
        twinCounter++;
        var ack = new WritableProperty<int>("interval")
        {
            Description = (client.Property_enabled?.Value == true) ? "desired notification accepted" : "disabled, not accepted",
            Status = (client.Property_enabled?.Value == true) ? 200 : 205,
            Version = req.Version,
            Value = req.Value
        };
        return await Task.FromResult(ack);
    }


    async Task<Cmd_getRuntimeStats_Response> Command_getRuntimeStats_Handler(Cmd_getRuntimeStats_Request req)
    {
        commandCounter++;
        var result = new Cmd_getRuntimeStats_Response()
        {
            Status = 200
        };

        //result.Add("runtime version", System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName ?? "n/a");
        result.Add("machine name", Environment.MachineName);
        result.Add("os version", Environment.OSVersion.ToString());
        if (req.DiagnosticsMode == DiagnosticsMode.complete)
        {
            result.Add("this app:", System.Reflection.Assembly.GetExecutingAssembly()?.FullName ?? "");
        }
        if (req.DiagnosticsMode == DiagnosticsMode.full)
        {
            result.Add($"twin counter: ", twinCounter.ToString());
            result.Add("telemetry counter: ", telemetryCounter.ToString());
            result.Add("command counter: ", commandCounter.ToString());
            result.Add("reconnects counter: ", reconnectCounter.ToString());
        }
        return await Task.FromResult(result);
    }

    private void RefreshScreen(object? state)
    {
        string RenderData()
        {
            void AppendLineWithPadRight(StringBuilder sb, string? s) => sb.AppendLine(s?.PadRight(Console.BufferWidth));

            string? enabled_value = client?.Property_enabled?.Value.ToString();
            string? interval_value = client?.Property_interval?.Value.ToString();
            StringBuilder sb = new();
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, client?.ConnectionSettings?.HostName);
            AppendLineWithPadRight(sb, $"{client?.ConnectionSettings?.DeviceId} ({client?.ConnectionSettings?.Auth})");
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, String.Format("{0:8} | {1:5} | {2}", "Property", "Value", "Version"));
            AppendLineWithPadRight(sb, String.Format("{0:8} | {1:5} | {2}", "--------", "-----", "------"));
            AppendLineWithPadRight(sb, String.Format("{0:8} | {1:5} | {2}", "enabled".PadRight(8), enabled_value?.PadLeft(5), client?.Property_enabled?.Version));
            AppendLineWithPadRight(sb, String.Format("{0:8} | {1:5} | {2}", "interval".PadRight(8), interval_value?.PadLeft(5), client?.Property_interval?.Version));
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, $"Reconnects: {reconnectCounter}");
            AppendLineWithPadRight(sb, $"Telemetry messages: {telemetryCounter}");
            AppendLineWithPadRight(sb, $"Twin messages: {twinCounter}");
            AppendLineWithPadRight(sb, $"Command messages: {commandCounter}");
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, $"WorkingSet: {telemetryWorkingSet.Bytes()}");
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, $"Time Running: {TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds).Humanize(3)}");
            AppendLineWithPadRight(sb, " ");
            AppendLineWithPadRight(sb, " ");
            return sb.ToString();
        }

        Console.SetCursorPosition(0, 0);
        Console.WriteLine(RenderData());
        screenRefresher = new Timer(RefreshScreen, this, 1000, 0);
    }
}