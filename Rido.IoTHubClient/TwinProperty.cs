using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    internal class TwinProperty<T> where T: struct
    {
        string twinJson = String.Empty;
        public TwinProperty(string json) => twinJson = json;
        public WritableProperty<T> InitTwin(string propName, T defaultValue)
        {
            WritableProperty<T> result = null;
            var root = JsonNode.Parse(twinJson);
            var desired = root?["desired"];
            var reported = root?["reported"];

            int? desiredVersion = desired?["$version"]?.GetValue<int>();

            T? desired_Prop = desired[propName]?.GetValue<T>();
            T? reported_Prop = reported[propName]?["value"]?.GetValue<T>();
            int? reported_Prop_version = reported[propName]?["av"]?.GetValue<int>();
            if (desired_Prop.HasValue)
            {
                if (desiredVersion > reported_Prop_version ||
                    !reported_Prop.HasValue)
                {
                    result = new WritableProperty<T>(propName)
                    {
                        Value = desired_Prop.Value,
                        Version = desiredVersion.Value,
                        Status = 200,
                        Description = "propertyAccepted"
                    };
                }
            }
            else if (!reported_Prop.HasValue)
            {
                result = new WritableProperty<T>(propName)
                {
                    Value = defaultValue,
                    Version = desiredVersion.Value,
                    Status = 201,
                    Description = "init to default value"
                };
            }
            if (result == null && reported_Prop.HasValue)
            {
                result = new WritableProperty<T>(propName)
                {
                    Value = reported_Prop.Value,
                    Version = reported_Prop_version.Value,
                };
            }
            return result;
        }
    }
        
    
}
