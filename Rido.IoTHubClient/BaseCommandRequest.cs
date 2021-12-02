using System;
using System.Collections.Generic;
using System.Text;

namespace Rido.IoTHubClient
{
    public interface IBaseCommandRequest
    {
        public object DeserializeBody(string payload);
    }
}
