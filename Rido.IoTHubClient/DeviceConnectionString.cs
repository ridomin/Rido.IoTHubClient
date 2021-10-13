using System.Collections.Generic;
using System.Diagnostics;

namespace Rido.IoTHubClient
{
    public class DeviceConnectionString
    {
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
        public string Auth { get; set; } = "SAS";


        public DeviceConnectionString() { }
        public DeviceConnectionString(string cs) => ParseConnectionString(cs);

        private void ParseConnectionString(string cs)
        {
            string GetConnectionStringValue(IDictionary<string, string> dict, string propertyName)
            {
                if (!dict.TryGetValue(propertyName, out string value))
                {
                    Trace.TraceWarning($"The connection string is missing the property: {propertyName}");
                }
                return value;
            }

            IDictionary<string, string> map = cs.ToDictionary(';', '=');
            this.HostName = GetConnectionStringValue(map, nameof(this.HostName));
            this.DeviceId = GetConnectionStringValue(map, nameof(this.DeviceId));
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
            this.Auth = GetConnectionStringValue(map, nameof(this.Auth));
        }

        public override string ToString()
        {
            return $"HostName={HostName};DeviceId={DeviceId};SharedAccessKey={SharedAccessKey};Auth={Auth}";
        }
    }
}
