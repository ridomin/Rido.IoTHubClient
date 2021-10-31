using System;
using System.Text.Json.Serialization;

namespace Rido.IoTHubClient
{
    public class CommandResponse
    {
        [JsonIgnore]
        public int _status { get; set; }
        [JsonIgnore]
        public string _rid { get; set; }
        public string CommandName { get; set; }
        public object CommandResponsePayload { get; set; }
    }

    public class CommandRequest
    {
        public string _rid { get; set; }
        public string CommandName { get; set; }
        public string CommandPayload { get; set; }
    }
}
