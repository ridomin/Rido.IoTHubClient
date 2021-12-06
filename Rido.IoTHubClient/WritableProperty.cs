﻿using Rido.IoTHubClient.TopicBinders;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class WritableProperty<T>
    {
        public PropertyAck<T> PropertyValue;
        readonly string propertyName;
        readonly string componentName;
        readonly UpdateTwinBinder updateTwin;
        readonly DesiredUpdatePropertyBinder<T> desiredBinder;

        public Func<PropertyAck<T>, Task<PropertyAck<T>>> OnProperty_Updated
        {
            get => desiredBinder.OnProperty_Updated;
            set => desiredBinder.OnProperty_Updated = value;
        }

        public WritableProperty(IMqttConnection connection, string name, string component = "")
        {
            propertyName = name;
            componentName = component;
            updateTwin = new UpdateTwinBinder(connection);
            PropertyValue = new PropertyAck<T>(name, componentName);
            desiredBinder = new DesiredUpdatePropertyBinder<T>(connection, name, componentName);
        }

        public async Task UpdateTwinAsync() => await updateTwin.UpdateTwinAsync(this.PropertyValue.ToAck());

        public async Task InitPropertyAsync(string twin, T defaultValue, CancellationToken cancellationToken = default)
        {
            PropertyValue = PropertyAck<T>.InitFromTwin(twin, propertyName, componentName, defaultValue);
            if (desiredBinder.OnProperty_Updated != null && (PropertyValue.DesiredVersion > 1))
            {
                var ack = await desiredBinder.OnProperty_Updated.Invoke(PropertyValue);
                _ = updateTwin.UpdateTwinAsync(ack.ToAck(), cancellationToken);
                PropertyValue = ack;
            }
            else
            {
                _ = updateTwin.UpdateTwinAsync(PropertyValue.ToAck());
            }
        }
    }
}
