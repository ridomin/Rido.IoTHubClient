using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rido.IoTHubClient
{
    public class WritableProperty<T> where T : struct
    {
        string propName;
        public WritableProperty(string name)
        {
            propName = name;
        }
        public int Version { get; set; }
        public string Description { get; set; }
        public int Status { get; set; }
        public T? Value { get; set; } = default(T);

        public static WritableProperty<T> InitFromTwin(string twinJson, string propName, T defaultValue)
        {
            WritableProperty<T> result = null;
            var root = JsonNode.Parse(twinJson);
            var desired = root?["desired"];
            var reported = root?["reported"];

            int? desiredVersion = desired?["$version"]?.GetValue<int>();

            T? desired_Prop = desired[propName]?.GetValue<T>();
            T? reported_Prop = reported[propName]?["value"]?.GetValue<T>();
            int? reported_Prop_version = reported[propName]?["av"]?.GetValue<int>();
            int? reported_Prop_status = reported[propName]?["ac"]?.GetValue<int>();
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
                    Status = reported_Prop_status.Value
                };
            }
            return result;

        }

        public string ToAck()
        {
            using MemoryStream ms = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WritePropertyName(propName);
            writer.WriteStartObject();

            writer.WriteString("ad", Description);
            writer.WriteNumber("av", Version );
            writer.WriteNumber("ac", Status);

            // TODO: Use pattern matching
            if (Value?.GetType() == typeof(bool)) writer.WriteBoolean("value", Convert.ToBoolean(Value));
            if (Value?.GetType() == typeof(int)) writer.WriteNumber("value", Convert.ToInt32(Value));
            if (Value?.GetType() == typeof(double)) writer.WriteNumber("value", Convert.ToDouble(Value));
            if (Value?.GetType() == typeof(DateTime)) writer.WriteString("value", Convert.ToDateTime(Value).ToString("yyyy-MM-ddThh:mm:ss.000Z"));
            if (Value?.GetType() == typeof(string)) writer.WriteString("value", Value.ToString());

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using StreamReader sr = new StreamReader(ms);
            return sr.ReadToEnd();
        }
    }
}