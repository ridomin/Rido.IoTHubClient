using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class TwinProperties
    {
        public static string BuildAck(string propsJson, int version, int status, string description = "")
        {
            using MemoryStream ms = new MemoryStream();
            using JsonDocument doc = JsonDocument.Parse(propsJson);
            using Utf8JsonWriter writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            foreach (var el in doc.RootElement.EnumerateObject())
            {
                if (!el.Name.StartsWith('$'))
                {
                    writer.WritePropertyName(el.Name);
                    writer.WriteStartObject();
                    writer.WriteNumber("ac", status);
                    writer.WriteNumber("av", version);
                    writer.WriteString("ad", description);
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

        public static string RemoveVersion(string inJson)
        {
            using MemoryStream ms = new MemoryStream();

            using JsonDocument doc = JsonDocument.Parse(inJson);
            using Utf8JsonWriter writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            foreach (var el in doc.RootElement.EnumerateObject())
            {
                if (!el.Name.StartsWith('$'))
                {
                    el.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using StreamReader sr = new StreamReader(ms);
            return sr.ReadToEnd();
        }
    }
}
