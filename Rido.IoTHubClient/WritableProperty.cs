using System;
using System.IO;
using System.Text.Json;

namespace Rido.IoTHubClient
{
    public class WritableProperty<T>
    {
        string propName;
        public WritableProperty(string name)
        {
            propName = name;
        }
        public int Version { get; set; }
        public string Description { get; set; }
        public int Status { get; set; }
        public T Value { get; set; } = default(T);

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
            if (Value?.GetType() == typeof(double)) writer.WriteNumber("value", Convert.ToInt32(Value));
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