﻿//  <auto-generated/> 
#nullable enable

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Protocol;
using Rido.IoTHubClient;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

public class CommandResponse
{
    [JsonIgnore]
    public int _status { get; set; }
    [JsonIgnore]
    public int _rid { get; set; }
}

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

public class Command_getMaxMinReport_Response : CommandResponse
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
    ConnectionSettings? dcs = null;

    public Func<TargetTemperature, PropertyAck>? OntargetTemperatureUpdated = null;

    public Func<Command_getMaxMinReport_Request, Command_getMaxMinReport_Response>? Command_getMaxMinReport = null;


    public Thermostat(string cs)
    {
        client = new MqttFactory().CreateMqttClient();
        dcs = ConnectionSettings.FromConnectionString(cs);
        var connack = client.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey, "dtmi:com:example:Thermostat;1", 5).Result;
        Console.WriteLine(connack.ResultCode);
        Configure().Wait();
    }

    public Thermostat(IMqttClient c, ConnectionSettings cs)
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

        client.UseApplicationMessageReceivedHandler(async m =>
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
                var ack = OntargetTemperatureUpdated?.Invoke(new TargetTemperature { targetTemperature = targetTemperature.GetDouble(), version = twinVersion });
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
                await client.PublishAsync($"$iothub/methods/res/{resp?._status}/?$rid={resp?._rid}", JsonSerializer.Serialize(resp));
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


    public async Task Report_targetTemperatureACK(PropertyAck ack)
    {
        var puback = await client.PublishAsync(
           $"$iothub/twin/PATCH/properties/reported/?$rid={lastRid++}", ack.BuildAck());
    }

    Action<string>? getTwin_cb;
    public async Task<string> GetTwin()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var puback = await client.PublishAsync($"$iothub/twin/GET/?$rid={lastRid++}", string.Empty);
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