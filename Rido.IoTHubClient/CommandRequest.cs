using System;

namespace Rido.IoTHubClient
{

    public class CommandRequest
    { 
        public string CommandName { get; set; }
        public string CommandPayload { get; set; }
    }
}
