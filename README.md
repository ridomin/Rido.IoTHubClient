# Rido.IoTHubClient

Minimalistic device client to interact with Azure IoT Hub based on [MQTTNet](https://github.com/chkr1011/MQTTnet)

[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)

[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg?branch=preview)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)

## Features

- V1, V2 Auth scheme, X509 + SaS
- Telemetry, Properties and Commands using reserved topics
- Pub/Sub to broker (only on hubs with broker enabled)

## Connect to IoTHub

Connect With SaS

```cs
var cs = Environment.GetEnvironmentVariable("cs");
var client = await HubMqttClient.CreateFromConnectionStringAsync(cs);
```
Connect with X509

```cs
var client = await HubMqttClient.CreateWithClientCertsAsync(
            "<hubname>.azure-devices.net",
            "<pathTo.pfx>", "<PFX Pwd>");
```
## Reserved Topics Usage

Send Telemetry

```cs
await client.SendTelemetryAsync(new { temperature = 1 });
```

Read Twin

```cs
var twin = await client.GetTwinAsync();
```


Update Twin (Reported Properties)

```cs
var version = await client.UpdateTwinAsync(new { tool = "from Rido.IoTHubClient" }); 
Console.WriteLine("Twin PATCHED version: " + version));
```

Respond to Twin updates (Desired Properties)

```cs
client.OnPropertyReceived += async (s, e) => 
{
    Console.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
    await Task.Delay(500);
    var puback = await client.UpdateTwinAsync(new { tool = new { ac = 200, av = e.Version, ad = "updated", value = "put value here" } });
};
```

Respond to Commands

```cs
client.OnCommandReceived += async (s, e) =>
{
    Console.WriteLine($"Processing Command {e.CommandName}");
    await Task.Delay(500);
    await client.CommandResponseAsync(e.Rid, e.CommandName, new { myResponse = "ok" }, "200");
};

```

# Tracing

This library uses `System.Diagnostics.Tracing`

```cs
Trace.Listeners[0].Filter = new EventTypeFilter(SourceLevels.Information);
Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
Trace.Listeners[1].Filter = new EventTypeFilter(SourceLevels.Warning);
```

## Custom Topics Usage

> Custom topics require IoTHub V2, available in the `preview` branch

When connected to a MQTTBroker enabled hub, this library allows to pub/sub to topics defined in the hub topic-space.

Using MQTTNet directly
```cs
var connack = await mqttClient.ConnectV2WithSasAsync(hostname, deviceId, DefaultKey);
```

With the Client implementing v2 reserved topics
```cs
var cs = Environment.GetEnvironmentVariable("cs");
var client = await HubBrokerMqttClient.CreateFromConnectionStringAsync(cs);
```


```cs

client.OnMessageReceived += (s, e) =>
{
    string payload = (e.ApplicationMessage.Topic);
};

await client.SubscribeAsync("vehicles/#");
await client.PublishAsync($"vehicles/{client.ClientId}/GPS/pos",
                            new { lat = 23.32323, lon = 54.45454 });
```

to create this topic spaces, use

```bash
az iot hub topic-space create -n {iothub_name} --tsn publisher_ts --tst PublishOnly --template 'vehicles/${principal.deviceid}/GPS/#'
az iot hub topic-space create -n {iothub_name} --tsn subscriber_ts --tst LowFanout --template 'vehicles/#'

```