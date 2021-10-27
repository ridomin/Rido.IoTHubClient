# Rido.IoTHubClient

Minimalistic device client to interact with Azure IoT Hub based on [MQTTNet](https://github.com/chkr1011/MQTTnet)

[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)

[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg?branch=preview)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)

## Features

- Device Auth for Devices and Modules, X509 + SaS with refresh tokens
- V1 support in master (V2 available in the `preview` branch, enabling Pub/Sub to MQTT Broker)
- DPS Client
- Telemetry, Properties and Commands using reserved topics for v1 and v2
- Extended Connection String support

## Connect to IoTHub

Connect Device With SaS

```cs
var client = await HubMqttClient.CreateAsync(hostname, device, sasKey);
```

Connect Module With SaS

```cs
var client = await HubMqttClient.CreateAsync(hostname, device, module, sasKey);
```

Announce the model Id

```cs
var client = await HubMqttClient.CreateAsync(hostname, device, sasKey, modelId);
```

Connect Device or Module with X509

```cs
var client = await HubMqttClient.CreateWithClientCertsAsync(hostname, certificate);
```

> Note: See [connection settings reference](#connection-settings-reference)

### Disconnects

The client will trigger the event ` event EventHandler<MqttClientDisconnectedEventArgs> OnMqttClientDisconnected;`

(also visible in diagnostics traces)


### MQTT Extensions

You can also connect with the MQTTNet client by using the extension methods:

```
var connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, sasKey);
var connack = await mqttClient.ConnectWithX509Async(hostname, certificate);
```

### DPS Support

```cs
var dpsRes = await DpsClient.ProvisionWithSasAsync("<IdScope>", "<deviceId>", "<deviceKey>");
Console.WriteLine(dpsRes.registrationState.assignedHub));
```

```cs
var dpsRes = await DpsClient.ProvisionWithCertAsync("<IdScope>", certificate);
Console.WriteLine(dpsRes.registrationState.assignedHub));
```


## Reserved Topics Usage

### Send Telemetry

```cs
await client.SendTelemetryAsync(new { temperature = 1 });
```

### Read Twin

```cs
var twin = await client.GetTwinAsync();
```


### Update Twin (Reported Properties)

```cs
var version = await client.UpdateTwinAsync(new { tool = "from Rido.IoTHubClient" }); 
Console.WriteLine("Twin PATCHED version: " + version));
```

### Respond to Twin updates (Desired Properties)

```cs
client.OnPropertyReceived += async (s, e) => 
{
    Console.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
    await Task.Delay(500);
    var puback = await client.UpdateTwinAsync(new { tool = new { ac = 200, av = e.Version, ad = "updated", value = "put value here" } });
};
```

### Respond to Commands

```cs
client.OnCommandReceived += async (s, e) =>
{
    Console.WriteLine($"Processing Command {e.CommandName}");
    await Task.Delay(500);
    await client.CommandResponseAsync(e.Rid, e.CommandName, new { myResponse = "ok" }, "200");
};

```

# Connection Settings Reference

This library implements a compatible *connection string* with Azure IoT SDK Device Client, and adds some new properties:

- `HostName` Azure IoT Hub hostname (FQDN)
- `DeviceId` Device Identity 
- `SharedAccessKey` Device Shared Access Key
- `X509Key` <pathtopfx>|<pfxpassword>
- `ModelId` DTDL Model ID in DTMI format to create PnP Devices
- `ModuleId` Device Module Identity
- `Auth` Device Authentication: [SAS, X509]
- `SasMinutes` SasToken expire time in minutes
- `RetryInterval` Wait before connection retries in seconds. 0 to disable automatic reconnects
- `MaxRetries` Max number of retries in case of automatic reconnect

Sample Connection String

```cs
$"HostName=test.azure-devices.net;DeviceId=myDevice;ModuleId=myModule;SharedAccessKey=<moduleSasKey>;ModelId=dtmi:my:model;1";SasMinutes=120
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
var connack = await mqttClient.ConnectV2WithSasAsync(hostname, deviceId, sasKey);
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

To create topic spaces, use

```bash
az iot hub topic-space create -n {iothub_name} --tsn publisher_ts --tst PublishOnly --template 'vehicles/${principal.deviceid}/GPS/#'
az iot hub topic-space create -n {iothub_name} --tsn subscriber_ts --tst LowFanout --template 'vehicles/#'

```