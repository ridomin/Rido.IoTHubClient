using System.Text.Json.Serialization;

namespace Rido.IoTHubClient
{
    public class CommandResponse
    {
        [JsonIgnore]
        public int Status { get; set; }
        [JsonIgnore]
        public string Rid { get; set; }
        public string CommandName { get; set; }
        public object CommandResponsePayload { get; set; }
    }
}
