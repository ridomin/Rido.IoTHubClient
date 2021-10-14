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
        public string ModelId { get; set; }
        public string ModuleId { get; set; }


        public DeviceConnectionString() { }
        public DeviceConnectionString(string cs) => ParseConnectionString(cs);

        private void ParseConnectionString(string cs)
        {
            string GetConnectionStringValue(IDictionary<string, string> dict, string propertyName, bool warnIfNotFound = true)
            {
                if (!dict.TryGetValue(propertyName, out string value))
                {
                    if (warnIfNotFound)
                    {
                        Trace.TraceWarning($"The connection string is missing the property: {propertyName}");
                    }
                }
                return value;
            }

            IDictionary<string, string> map = cs.ToDictionary(';', '=');
            this.HostName = GetConnectionStringValue(map, nameof(this.HostName));
            this.DeviceId = GetConnectionStringValue(map, nameof(this.DeviceId));
            this.ModuleId= GetConnectionStringValue(map, nameof(this.ModuleId), false);
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
            this.Auth = GetConnectionStringValue(map, nameof(this.Auth));
            this.ModelId= GetConnectionStringValue(map, nameof(this.ModelId));
            this.Auth = GetConnectionStringValue(map, nameof(this.Auth), false);
        }

        public override string ToString()
        {
            var result = $"HostName={HostName};DeviceId={DeviceId}";
            
            if (!string.IsNullOrEmpty(ModuleId))
            {
                result += $";ModuleId={ModuleId}";
            }

            result +=  $";SharedAccessKey={SharedAccessKey};Auth={Auth}";
            return result;
        }
    }
}
