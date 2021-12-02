using System;
using System.Collections.Generic;
using System.Text;

namespace Rido.IoTHubClient
{

    // from https://stackoverflow.com/a/66927384/2158571
    public interface IBaseCommandRequest
    {
        public int _rid { get; set; }
        public abstract object Deserialize(string payload);
    }
}
