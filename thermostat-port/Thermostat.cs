using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using Rido.IoTHubClient;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Web;


public class TargetTemperatureArgs : EventArgs
{
    public double targetTemperature { get; set; }
    public int version { get; set; }
}

public class Command_getMaxMinReport_Request
{
    public DateTime since { get; set; }
}

public class Command_getMaxMinReport_Response
{
    public double maxTemp { get; set; }
    public double minTemp { get; set; }
    public double avgTemp { get; set; }
    public DateTimeOffset startTime { get; set; }
    public DateTimeOffset endTime { get; set; }


}

public class Thermostat
{
    int lastRid = 0;
    IMqttClient? client = null;
    DeviceConnectionString? dcs = null;

    public event EventHandler<TargetTemperatureArgs>? OntargetTemperatureUpdated = null;
    
    public Func<Command_getMaxMinReport_Request, Command_getMaxMinReport_Response>? Command_getMaxMinReport = null;

    

    public Thermostat(string cs)
    {
        client = IMqttClientExtensions.CreateMqttClientWithLogger(Console.Out);
        dcs = new DeviceConnectionString(cs);
        var connack = client.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, "dtmi:com:example:Thermostat;1", 5).Result;
        Console.WriteLine(connack.ResultCode);
        Configure().Wait();
    }

    public Thermostat(IMqttClient c, DeviceConnectionString cs)
    {
        client = c;
        dcs = cs ?? throw new ArgumentNullException(nameof(cs));
        Configure().Wait();
    }

    async Task Configure()
    {
        var subres = await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                                                   .WithTopicFilter("$iothub/methods/POST/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                   .WithTopicFilter("$iothub/twin/res/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                   .WithTopicFilter("$iothub/twin/PATCH/properties/desired/#", MqttQualityOfServiceLevel.AtMostOnce)
                                                   .Build());
        subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

        if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
        {
            throw new ApplicationException("Error subscribing to system topics");
        }

        client.UseApplicationMessageReceivedHandler(m =>
        {
            var segments = m.ApplicationMessage.Topic.Split('/');
            int rid = 0;
            int twinVersion = 0;
            if (m.ApplicationMessage.Topic.Contains("?"))
            {
                // parse qs to extract the rid
                var qs = HttpUtility.ParseQueryString(segments[^1]);
                rid = Convert.ToInt32(qs["$rid"]);
                twinVersion = Convert.ToInt32(qs["$version"]);
            }

            string msg = string.Empty;
            if (m.ApplicationMessage.Topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
            {
                msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                JsonElement targetTemperature = JsonDocument.Parse(msg).RootElement.GetProperty("targetTemperature");
                OntargetTemperatureUpdated?.Invoke(this, new TargetTemperatureArgs { targetTemperature = targetTemperature.GetDouble(), version = twinVersion });
            }

            if (m.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/204"))
            {
                msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                Callback_maxTempSinceLastReboot?.Invoke(twinVersion);
            }

            if (m.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/getMaxMinReport"))
            {
                msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                Command_getMaxMinReport?.Invoke(new Command_getMaxMinReport_Request { since = JsonSerializer.Deserialize<DateTime>(msg) });
            }
        });
    }

    Action<int>? Callback_maxTempSinceLastReboot = null;
    public async Task<int> Report_maxTempSinceLastReboot(double temperature)
    {
        var tcs = new TaskCompletionSource<int>();
        var puback = await client.PublishAsync(
            $"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}",
            JsonSerializer.Serialize(new { maxTempSinceLastReboot = temperature }));
        if (puback.ReasonCode == MqttClientPublishReasonCode.Success)
        {
            Callback_maxTempSinceLastReboot = s => tcs.TrySetResult(s);
        }
        else
        {
            Callback_maxTempSinceLastReboot = s => tcs.TrySetException(new ApplicationException($"Error '{puback.ReasonCode}' publishing twin PATCH: {s}"));
        }
        return tcs.Task.Result;
    }

    public async Task<MqttClientPublishResult> Send_temperature(double temperature)
    {
        return await client.PublishAsync(
            $"devices/{dcs?.DeviceId}/messages/events/",
            JsonSerializer.Serialize(new { temperature }));
    }

    internal async Task Ack_TargetTemperature(double temperature, int status, int version)
    {
        var puback = await client.PublishAsync(
           $"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}",
           JsonSerializer.Serialize(new { targetTemperature = new { ac = status, av = version, value = temperature } }));
    }
}

