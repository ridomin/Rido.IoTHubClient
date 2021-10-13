using System;

namespace Rido.IoTHubClient
{
    public class CommandEventArgs : EventArgs
    {
        public string Rid { get; set; }
        public string CommandName { get; set; }
        public string CommandRequestMessageJson { get; set; }
        public string Topic { get; set; }
    }
}
