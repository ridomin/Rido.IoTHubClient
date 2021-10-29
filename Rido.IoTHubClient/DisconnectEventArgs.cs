using System;

namespace Rido.IoTHubClient
{
    public class DisconnectEventArgs : EventArgs
    {
        public ConnResultCode? ResultCode { get; set; }
        public Exception? Exception { get; set; }
        public DisconnectReason? DisconnectReason { get; set; }
    }

}