using System;

namespace Rido.IoTHubClient
{
    public class PropertyEventArgs : EventArgs
    {
        public string Rid { get; set; }
        public string PropertyMessageJson { get; set; }
        public string Topic { get; set; }
        public int Version { get; set; }
    }
}
