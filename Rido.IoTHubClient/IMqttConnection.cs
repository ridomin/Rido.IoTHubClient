﻿using MQTTnet;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public interface IMqttConnection : IDisposable
    {
        ConnectionSettings ConnectionSettings { get; }
        bool IsConnected { get; }
        event EventHandler<DisconnectEventArgs> OnMqttClientDisconnected;
        Task CloseAsync();

        Task<MqttClientSubscribeResult> SubscribeAsync(string[] topics);
        Task<MqttClientPublishResult> PublishAsync(string topic, object payload);
        Task<MqttClientPublishResult> PublishAsync(string topic, object payload, CancellationToken cancellationToken);
        Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessage { get; set; }
    }
}