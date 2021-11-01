using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rido.IoTHubClient
{
    public class PropertyReceived
    {
        public string Rid { get; set; }
        public string PropertyMessageJson { get; set; }
        public string Topic { get; set; }
        public int Version { get; set; }

    }

    public class PropertyAck
    {
        public int Version { get; set; }
        public int Status { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }

        public string BuildAck()
        {
            using MemoryStream ms = new MemoryStream();
            using JsonDocument doc = JsonDocument.Parse(Value);
            using Utf8JsonWriter writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            foreach (var el in doc.RootElement.EnumerateObject())
            {
                if (!el.Name.StartsWith('$'))
                {
                    writer.WritePropertyName(el.Name);
                    writer.WriteStartObject();
                    writer.WriteNumber("ac", Status);
                    writer.WriteNumber("av", Version);
                    writer.WriteString("ad", Description);
                    writer.WriteString("value", el.Value.ToString());
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using StreamReader sr = new StreamReader(ms);
            return sr.ReadToEnd();
        }
    }


    public class PropertyEventArgs : EventArgs
    {
        public string Rid { get; set; }
        public string PropertyMessageJson { get; set; }
        public string Topic { get; set; }
        public int Version { get; set; }
    }
}
