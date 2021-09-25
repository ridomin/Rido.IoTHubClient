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
