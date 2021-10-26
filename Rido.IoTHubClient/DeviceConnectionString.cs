using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Rido.IoTHubClient
{
    public class DeviceConnectionString
    {
        public string IdScope { get; set; }    
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
        public string X509Key { get; set; } //paht-to.pfx|pfxpwd
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
            this.IdScope = GetConnectionStringValue(map, nameof(this.IdScope));
            this.HostName = GetConnectionStringValue(map, nameof(this.HostName));
            this.DeviceId = GetConnectionStringValue(map, nameof(this.DeviceId));
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
            this.ModuleId = GetConnectionStringValue(map, nameof(this.ModuleId), false);
            this.X509Key = GetConnectionStringValue(map, nameof(this.X509Key), false);
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
            if (!string.IsNullOrEmpty(this.SharedAccessKey))
            {
                this.Auth = "SAS";
            }
            if (!string.IsNullOrEmpty(this.X509Key))
            {
                this.Auth = "X509";
            }
        }

        public override string ToString()
        {
            void AppendIfNotEmpty(StringBuilder sb, string name, string val)
            {
                if (!string.IsNullOrEmpty(val))
                {
                    if (name.Contains("Key"))
                    {
                        sb.Append($";{name}=***");
                    }
                    else
                    {
                        sb.Append($";{name}={val}");
                    }
                }
            }

            var result = new StringBuilder();
            result.Append($"DeviceId={DeviceId}");
            AppendIfNotEmpty(result, nameof(this.IdScope), IdScope);
            AppendIfNotEmpty(result, nameof(this.HostName), HostName);
            AppendIfNotEmpty(result, nameof(this.ModuleId), ModuleId);
            AppendIfNotEmpty(result, nameof(this.SharedAccessKey), SharedAccessKey);
            AppendIfNotEmpty(result, nameof(this.ModelId), ModelId);
            AppendIfNotEmpty(result, nameof(this.SasMinutes), SasMinutes.ToString());
            AppendIfNotEmpty(result, nameof(this.RetryInterval), RetryInterval.ToString());
            AppendIfNotEmpty(result, nameof(this.X509Key), X509Key);
            AppendIfNotEmpty(result, nameof(this.Auth), Auth);
            return result.ToString();
        }
    }
}
