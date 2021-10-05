# Rido.IoTHubClient

Minimalistic device client to interact with Azure IoT Hub

## Features

- V2 Auth scheme, X509 + SaS
- Telemetry, Properties and Commands using the new topics
- Pub/Sub to broker

## Connect to V2 Hubs

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

## Custom Topics Usage

When connected to a MQTTBroker enabled hub, this library allows to pub/sub to topics defined in the hub topic-space.

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
    await client.UpdateTwinAsync(new { tool = new { ac = 200, av = e.Version, ad = "updated", value = "put value here" } }, 
    v => Console.WriteLine("PATCHED ACK: " + v));
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

