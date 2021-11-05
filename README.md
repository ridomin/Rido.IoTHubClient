# Rido.IoTHubClient

Minimalistic device client to interact with Azure IoT Hub based on [MQTTNet](https://github.com/chkr1011/MQTTnet)

|master|preview|
|--|--|
|[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)|[![.NET](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml/badge.svg?branch=preview)](https://github.com/ridomin/Rido.IoTHubClient/actions/workflows/dotnet.yml)|

# Features

- Device Auth for Devices and Modules, X509 + SaS with token refresh and reconnects
- ClassicHub support in master 
- Broker-Enabled Hub available in the `preview` branch, with custom pub/sub support)
- DPS Client
- Telemetry, Properties and Commands using reserved topics for classic and broker-enabled hubs
- Single entry point for DPS, Hub and Central by using a common `ConnectionSettings`

## Connect to IoT Hub

### Authentication

- X509 and SAS Authentication available as `IMqttClient` extension methods.

```cs
mqttClient = MqttFactory().CreateMqttClient();
connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, sasKey);
connack = await mqttClient.ConnectWithSasAsync(hostname, deviceId, moduleId, sasKey);
connack = await mqttClient.ConnectWithX509Async(hostname, certificate);
```

> Note: To connect with module identity, certificates, PnP, or other connection option use the
 [connection settings ](#connection-settings-reference) overload.

### Connection Management

- The `HubMqttConnection` class handles Sas Token refresh and Reconnects, configurable using the `ConnectionSettings`.
- Disconnects trigger the event ` event EventHandler<MqttClientDisconnectedEventArgs> OnMqttClientDisconnected;`

```cs
var connection = await HubMqttConnection.CreateAsync(
  ConnectionSettings.FromConnectionString(
    Environment.GetEnvironmentVariable("cs")));
```


### DPS Support

When using a connection setting with `IdSCope` the HubMqttConnection class will provision and set the assigned hub to the connection setting.

```cs
var dpsRes = await DpsClient.ProvisionWithSasAsync("<IdScope>", "<deviceId>", "<deviceKey>");
Console.WriteLine(dpsRes.registrationState.assignedHub));
```

```cs
var dpsRes = await DpsClient.ProvisionWithCertAsync("<IdScope>", certificate);
Console.WriteLine(dpsRes.registrationState.assignedHub));
```

### Reserved Topics Usage

Reserved topics to use Telemetry, Properties and Commands are implemented in the `HubMqttClient`.

```cs
var client = await HubMqttClient.CreateAsync(
  ConnectionSettings.FromConnectionString(
    Environment.GetEnvironmentVariable("cs")));
```

#### Send Telemetry

```cs
await client.SendTelemetryAsync(new { temperature = 1 });
```

#### Read Twin

```cs
var twin = await client.GetTwinAsync();
```


#### Update Twin (Reported Properties)

```cs
var version = await client.UpdateTwinAsync(new { tool = "from Rido.IoTHubClient" }); 
Console.WriteLine("Twin PATCHED version: " + version));
```

#### Respond to Twin updates (Desired Properties)

```cs
client.OnPropertyChange = async e =>
{
    Console.WriteLine($"Processing Desired Property {e.PropertyMessageJson}");
    return new PropertyAck()
    {
        Version = e.Version,
        Status = 200,
        Description = "testing acks",
        Value = e.PropertyMessageJson
    };
};
```

#### Respond to Commands

```cs
client.OnCommand = async req => 
{
    System.Console.WriteLine($"<- Received Command {req.CommandName}");
    string payload = req.CommandPayload;
    System.Console.WriteLine(payload);
    return new CommandResponse
    {
        Status = 200,
        CommandResponsePayload = new { myResponse = "all good"}
    };
};
```

# Connection Settings Reference

This library implements a compatible *connection string* with Azure IoT SDK Device Client, and adds some new properties:

- `HostName` Azure IoT Hub hostname (FQDN)
- `IdScope` DPS IdScope 
- `DeviceId` Device Identity 
- `SharedAccessKey` Device Shared Access Key
- `X509Key` __pathtopfx>|<pfxpassword__
- `ModelId` DTDL Model ID in DTMI format to create PnP Devices
- `ModuleId` Device Module Identity
- `Auth` Device Authentication: [SAS, X509]
- `SasMinutes` SasToken expire time in minutes, default to `60`.
- `RetryInterval` Wait before connection retries in seconds. 0 to disable automatic reconnects, default to `5`.
- `MaxRetries` Max number of retries in case of automatic reconnect. default to `10`.

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

# Custom Topics Usage

Using custom topics requires access tp an IoTHub enabled broker *actually in provate preview*. 
Instructions available [here](https://github.com/Azure/IoTHubMQTTBrokerPreviewSamples#private-preview-program-information)

The `preview` branch implements the Authentication scheme and reserved topics.


Using MQTTNet directly
```cs
var mqttClient = new MqttFactory().CreateMqttClient(); 
var dcs = ConnectionSettings.FromConnectionString(cs);
System.Console.WriteLine(dcs);
var connack = await mqttClient.ConnectWithSasAsync(dcs.HostName, dcs.DeviceId, dcs.SharedAccessKey);
Console.WriteLine($"{nameof(mqttClient.IsConnected)}:{mqttClient.IsConnected} . {connack.ResultCode}");

var topic = $"vehicles";
var subAck = await mqttClient.SubscribeAsync(topic + $"/+/telemetry");
subAck.Items.ForEach(x => Console.WriteLine($"{x.TopicFilter}{x.ResultCode}"));

mqttClient.UseApplicationMessageReceivedHandler(e =>
{
    Console.Write($"<- {e.ApplicationMessage.Topic} {e.ApplicationMessage.Payload.Length} Bytes: ");
    Console.WriteLine(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
    //Console.Write('.');
});

while (true)
{
    string pubtopic = $"{topic}/{dcs.DeviceId}/telemetry";
    var msg = Environment.TickCount64.ToString();
    var pubAck = await mqttClient.PublishAsync(pubtopic, msg);
    Console.WriteLine($"-> {pubtopic} {msg}. {pubAck.ReasonCode}");
    await Task.Delay(1000);
}

```

To create topic spaces, use

```bash
az iot hub topic-space create -n {iothub_name} --tsn publisher_ts --tst PublishOnly --template 'vehicles/${principal.deviceid}/GPS/#'
az iot hub topic-space create -n {iothub_name} --tsn subscriber_ts --tst LowFanout --template 'vehicles/#'

```