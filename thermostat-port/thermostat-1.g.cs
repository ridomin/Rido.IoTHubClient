﻿//  <auto-generated/> 
#nullable enable

using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;



public class TargetTemperature 
{
    public double targetTemperature { get; set; }
    public int version { get; set; }
}

public class Command_getMaxMinReport_Request
{
    public DateTime since { get; set; }
    public int _rid { get; set; }
}
public class Command_getMaxMinReport_Response 
{
    [JsonIgnore]
    public int _status { get; set; }
    [JsonIgnore]
    public int _rid { get; set; }

    public double maxTemp { get; set; }
    public double minTemp { get; set; }
    public double avgTemp { get; set; }
    public DateTimeOffset startTime { get; set; }
    public DateTimeOffset endTime { get; set; }
}

public class Thermostat
{
    int lastRid = 0;
    internal HubMqttConnection connection ;

    public Func<TargetTemperature, Task<PropertyAck>>? OntargetTemperatureUpdated = null;

    public Func<Command_getMaxMinReport_Request, Command_getMaxMinReport_Response>? Command_getMaxMinReport = null;

    public static async Task<Thermostat> CreateAsync(string cs)
    {
        var dcs = new ConnectionSettings(cs) { ModelId = "dtmi:com:example:Thermostat;1" };
        var c = await HubMqttConnection.CreateAsync(dcs);
        Thermostat t = new Thermostat(c);
        await t.Configure();
        return t;
    }

    private Thermostat(HubMqttConnection conn)
    {
        connection = conn;
    }

    async Task Configure()
    {
        var subres = await connection.SubscribeAsync(new string[] {
                                                    "$iothub/methods/POST/#",
                                                    "$iothub/twin/res/#",
                                                    "$iothub/twin/PATCH/properties/desired/#"});

        subres.Items.ToList().ForEach(x => Trace.TraceInformation($"+ {x.TopicFilter.Topic} {x.ResultCode}"));

        if (subres.Items.ToList().Any(x => x.ResultCode == MqttClientSubscribeResultCode.UnspecifiedError))
        {
            throw new ApplicationException("Error subscribing to system topics");
        }

        connection.OnMessage = async m =>
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

            string msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
            if (m.ApplicationMessage.Topic.StartsWith("$iothub/twin/PATCH/properties/desired"))
            {
                JsonElement targetTemperature = JsonDocument.Parse(msg).RootElement.GetProperty("targetTemperature");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                var ack = await OntargetTemperatureUpdated?.Invoke(new TargetTemperature { targetTemperature = targetTemperature.GetDouble(), version = twinVersion });
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                if (ack != null) await this.Report_targetTemperatureACK(ack);
            }

            if (m.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/200"))
            {
                getTwin_cb?.Invoke(msg);
            }

            if (m.ApplicationMessage.Topic.StartsWith("$iothub/twin/res/204"))
            {
                msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                Callback_maxTempSinceLastReboot?.Invoke(twinVersion);
            }

            if (m.ApplicationMessage.Topic.StartsWith("$iothub/methods/POST/getMaxMinReport"))
            {
                msg = Encoding.UTF8.GetString(m.ApplicationMessage.Payload ?? Array.Empty<byte>());
                var resp = Command_getMaxMinReport?.Invoke(
                    new Command_getMaxMinReport_Request { since = JsonSerializer.Deserialize<DateTime>(msg), _rid = rid });
                await connection.PublishAsync($"$iothub/methods/res/{resp?._status}/?$rid={resp?._rid}", JsonSerializer.Serialize(resp));
            }
        };
    }



    Action<int>? Callback_maxTempSinceLastReboot = null;
    public async Task<int> Report_maxTempSinceLastReboot(double temperature)
    {
        var tcs = new TaskCompletionSource<int>();
        var puback = await connection.PublishAsync(
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
        return await connection.PublishAsync(
            $"devices/{connection.ConnectionSettings.DeviceId}/messages/events/",
            JsonSerializer.Serialize(new { temperature }));
    }


    public async Task Report_targetTemperatureACK(PropertyAck ack)
    {
        var puback = await connection.PublishAsync(
           $"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", ack.BuildAck());
    }

    Action<string>? getTwin_cb;
    public async Task<string> GetTwin()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var puback = await connection.PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
        if (puback?.ReasonCode == MqttClientPublishReasonCode.Success)
        {
            getTwin_cb = s => tcs.TrySetResult(s);
        }
        else
        {
            getTwin_cb = s => tcs.TrySetException(new ApplicationException($"Error '{puback?.ReasonCode}' publishing twin GET: {s}"));
        }
        return await tcs.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
    }

    public async Task<TargetTemperature> GetTargetTemperature()
    {
        TargetTemperature result = new TargetTemperature();
        var twinJson = await GetTwin();
        var twin = JsonDocument.Parse(twinJson).RootElement;
        var desired = twin.GetProperty("desired");
        if (desired.TryGetProperty("targetTemperature", out JsonElement targetTempNode))
        {
            if (targetTempNode.TryGetDouble(out double targetTempValue))
            {
                result.targetTemperature = targetTempValue;
            }
            if (desired.TryGetProperty("$version", out JsonElement versionEl))
            {
                result.version = versionEl.GetInt32();
            }
        }
        else
        {
            var reported = twin.GetProperty("reported");
            if (reported.TryGetProperty("targetTemperature", out JsonElement targetTempAck))
            {
                if (targetTempAck.TryGetProperty("value", out JsonElement targetTempAckValue))
                {
                    result.targetTemperature = targetTempAckValue.GetDouble();
                }
                if (targetTempAck.TryGetProperty("av", out JsonElement targetTempAckVersion))
                {
                    result.version = targetTempAckVersion.GetInt32();
                }
            }
        }
        return result;
    }
}