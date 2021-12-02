using Rido.IoTHubClient;
using Rido.IoTHubClient.TopicBinders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pnp_memmon
{
    public class Prop_interval : WritableProperty<int>
    {
        const string propName = "interval";
        UpdateTwinBinder updateTwin;
        public DesiredUpdateTwinBinder<int>? Property_interval_Desired = null;
        public Prop_interval(IMqttConnection connection) : base(propName)
        {
            Property_interval_Desired = new DesiredUpdateTwinBinder<int>(connection, propName);
            updateTwin = new UpdateTwinBinder(connection);
        }

        public static async Task Init_Async(string twin, int defaultInterval)
        {
            Prop_interval Property_interval = (Prop_interval)Prop_interval.InitFromTwin(twin, propName, defaultInterval);

            if (Property_interval != null && 
                Property_interval.Property_interval_Desired != null &&
                Property_interval.Property_interval_Desired.OnProperty_Updated != null &&
                (Property_interval.DesiredVersion > 1))
            {
                var ack = await Property_interval.Property_interval_Desired.OnProperty_Updated.Invoke(Property_interval);
                //_ = UpdateTwinBinder.S(ack.ToAck());
                //Property_interval = ack;
            }
            else
            {
                //_ = UpdateTwinAsync(Property_interval.ToAck());
            }
        }
    }
}
