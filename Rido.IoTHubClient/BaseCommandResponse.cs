using System.Text.Json.Serialization;

namespace Rido.IoTHubClient
{
    public abstract class BaseCommandResponse
    {
        [JsonIgnore]
        public int Status { get; set; }
    }
}
