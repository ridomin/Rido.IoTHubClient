using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Rido.IoTHubClient
{
    public class WritableProperty<T> // where T : struct
    {
        string propName;
        public WritableProperty(string name)
        {
            propName = name;
        }
        [JsonIgnore]
        public int? DesiredVersion { get; set; }
        [JsonPropertyName("av")]
        public int? Version { get; set; }
        [JsonPropertyName("ad")]
        public string Description { get; set; }
        [JsonPropertyName("ac")]
        public int Status { get; set; }
        [JsonPropertyName("value")]
        public T Value { get; set; } = default(T);

        public static WritableProperty<T> InitFromTwin(string twinJson, string propName, T defaultValue)
        {
            var root = JsonNode.Parse(twinJson);
            var desired = root?["desired"];
            var reported = root?["reported"];
            int desiredVersion = desired["$version"].GetValue<int>();
            WritableProperty<T> result = new WritableProperty<T>(propName) { DesiredVersion = desiredVersion };

            bool desiredFound = false;
            T desired_Prop = default(T);
            if (desired[propName] != null)
            {
                desired_Prop = desired[propName].GetValue<T>();
                desiredFound = true;
            }

            bool reportedFound = false;
            T reported_Prop = default(T);
            int reported_Prop_version = 0;
            int reported_Prop_status = 001;
            string reported_Prop_description = String.Empty;

            if (reported[propName] != null)
            {
                reported_Prop = reported[propName]["value"].GetValue<T>();
                reported_Prop_version = reported[propName]["av"]?.GetValue<int>() ?? -1;
                reported_Prop_status = reported[propName]["ac"].GetValue<int>();
                reported_Prop_description = reported[propName]["ad"]?.GetValue<string>();
                reportedFound = true;
            }

            if (!desiredFound && !reportedFound)
            {
                result = new WritableProperty<T>(propName)
                {
                    DesiredVersion = desiredVersion,
                    Version = reported_Prop_version,
                    Value = defaultValue,
                    Status = 201,
                    Description = "Init from default value"
                };
            }

            if (!desiredFound && reportedFound)
            {
                result = new WritableProperty<T>(propName)
                {
                    DesiredVersion = 0,
                    Version = reported_Prop_version,
                    Value = reported_Prop,
                    Status = reported_Prop_status,
                    Description = reported_Prop_description
                };
            }

            if (desiredFound && reportedFound)
            {
                if (desiredVersion >= reported_Prop_version)
                {
                    result = new WritableProperty<T>(propName)
                    {
                        DesiredVersion = desiredVersion,
                        Value = desired_Prop,
                        Version = desiredVersion
                    };
                }
            }


            if (desiredFound && !reportedFound)
            {
                result = new WritableProperty<T>(propName)
                {
                    DesiredVersion = desiredVersion,
                    Version = desiredVersion,
                    Value = desired_Prop
                };
            }


            return result;
        }

        public string ToAck() => JsonSerializer.Serialize(new Dictionary<string, object>() { { propName, this } });
    }
}