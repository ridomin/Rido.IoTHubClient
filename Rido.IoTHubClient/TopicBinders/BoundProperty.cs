using System;
using System.Threading.Tasks;

namespace Rido.IoTHubClient.TopicBinders
{
    public class Bound_Property<T>
    {
        public WritableProperty<T> PropertyValue;
        readonly string propertyName;
        readonly UpdateTwinBinder updateTwin;
        readonly DesiredUpdatePropertyBinder<T> desiredBinder;

        public Func<WritableProperty<T>, Task<WritableProperty<T>>> OnProperty_Updated
        {
            get => desiredBinder.OnProperty_Updated;
            set => desiredBinder.OnProperty_Updated = value;
        }

        public Bound_Property(IMqttConnection connection, string name, string componentName = "")
        {
            propertyName = name;
            updateTwin = new UpdateTwinBinder(connection);
            PropertyValue = new WritableProperty<T>(name, componentName);
            desiredBinder = new DesiredUpdatePropertyBinder<T>(connection, name, componentName);
        }

        public async Task UpdateTwinAsync() => await updateTwin.UpdateTwinAsync(this.PropertyValue.ToAck());

        public async Task InitPropertyAsync(string twin, T defaultValue)
        {
            PropertyValue = WritableProperty<T>.InitFromTwin(twin, propertyName, defaultValue);
            if (desiredBinder.OnProperty_Updated != null && (PropertyValue.DesiredVersion > 1))
            {
                var ack = await desiredBinder.OnProperty_Updated.Invoke(PropertyValue);
                _ = updateTwin.UpdateTwinAsync(ack.ToAck());
                PropertyValue = ack;
            }
            else
            {
                _ = updateTwin.UpdateTwinAsync(PropertyValue.ToAck());
            }
        }
    }
}
