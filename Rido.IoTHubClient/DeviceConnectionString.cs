using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rido.IoTHubClient
{
    public class DeviceConnectionString
    {
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
        public string ModelId { get; set; }
        public string ModuleId { get; set; }
        public string Auth { get; set; } = "SAS";
        public int SasMinutes { get; set; } = 60;
        public int RetryInterval { get; set; }


        public DeviceConnectionString() { }
        public DeviceConnectionString(string cs) => ParseConnectionString(cs);

        private void ParseConnectionString(string cs)
        {
            static string GetConnectionStringValue(IDictionary<string, string> dict, string propertyName, bool warnIfNotFound = true)
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
            this.ModuleId = GetConnectionStringValue(map, nameof(this.ModuleId), false);
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
            this.ModelId = GetConnectionStringValue(map, nameof(this.ModelId), false);
            this.Auth = GetConnectionStringValue(map, nameof(this.Auth), false);
            var sasMinutesValue = GetConnectionStringValue(map, nameof(this.SasMinutes), false);
            if (!string.IsNullOrEmpty(sasMinutesValue))
            {
                this.SasMinutes = Convert.ToInt32(sasMinutesValue);
            }
            var retryInterval = GetConnectionStringValue(map, nameof(this.RetryInterval), false);
            if (!string.IsNullOrEmpty(retryInterval))
            {
                var intRetryInterval =  Convert.ToInt32(retryInterval);
                if (intRetryInterval > 0)
                {
                    this.RetryInterval = intRetryInterval;
                } 
                    
            }
        }

        public override string ToString()
        {
            var result = $"HostName={HostName};DeviceId={DeviceId}";

            if (!string.IsNullOrEmpty(ModuleId))
            {
                result += $";ModuleId={ModuleId}";
            }

            if (!string.IsNullOrEmpty(ModelId))
            {
                result += $";ModelId={ModelId}";
            }
            if (Auth == "SAS")
            {
                result += $";SharedAccessKey=***";
                result += $";SasMinutes={SasMinutes};Auth={Auth}";
            }
            else
            {
                result += $";Auth={Auth}";
            }

            return result;
        }
    }
}
