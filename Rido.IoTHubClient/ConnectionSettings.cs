using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Rido.IoTHubClient
{
    public class ConnectionSettings
    {
        const int Default_SasMinutes = 60;
        const int Default_RetryInterval = 0;
        const int Default_MaxRetries = 10;

        public string IdScope { get; set; }    
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
        public string X509Key { get; set; } //paht-to.pfx|pfxpwd
        public string ModelId { get; set; }
        public string ModuleId { get; set; }
        public string Auth { get; set; }
        public int SasMinutes { get; set; }
        public int RetryInterval { get; set; }
        public int MaxRetries { get; set; }

        private ConnectionSettings(string cs) => ParseConnectionString(cs);
        public ConnectionSettings() 
        {
            this.SasMinutes = Default_SasMinutes;
            this.RetryInterval = Default_RetryInterval;
            this.MaxRetries = Default_RetryInterval;
        }
        public static ConnectionSettings FromConnectionString(string cs) => new ConnectionSettings(cs);

        private void ParseConnectionString(string cs)
        {
            static string GetConnectionStringValue(IDictionary<string, string> dict, string propertyName, bool logIfNotFound = false)
            {
                if (!dict.TryGetValue(propertyName, out string value))
                {
                    if (logIfNotFound)
                    {
                        Trace.TraceInformation($"The connection string is missing the property: {propertyName}");
                    }
                }
                return value;
            }

            IDictionary<string, string> map = cs.ToDictionary(';', '=');
            this.IdScope = GetConnectionStringValue(map, nameof(this.IdScope), true);
            this.HostName = GetConnectionStringValue(map, nameof(this.HostName), true);
            this.DeviceId = GetConnectionStringValue(map, nameof(this.DeviceId), true);
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
            this.ModuleId = GetConnectionStringValue(map, nameof(this.ModuleId));
            this.X509Key = GetConnectionStringValue(map, nameof(this.X509Key));
            this.ModelId = GetConnectionStringValue(map, nameof(this.ModelId));
            this.Auth = GetConnectionStringValue(map, nameof(this.Auth));

            var sasMinutesValue = GetConnectionStringValue(map, nameof(this.SasMinutes));
            if (string.IsNullOrEmpty(sasMinutesValue))
            {
                this.SasMinutes = Default_SasMinutes;
            }
            else
            {
                this.SasMinutes =  Convert.ToInt32(sasMinutesValue);
            }

            var retryInterval = GetConnectionStringValue(map, nameof(this.RetryInterval));
            if (string.IsNullOrEmpty(retryInterval))
            {
                this.RetryInterval = Default_RetryInterval;
            }
            else
            {
                var intRetryInterval =  Convert.ToInt32(retryInterval);
                if (intRetryInterval > 0)
                {
                    this.RetryInterval = intRetryInterval;
                } 
            }

            var maxRetries = GetConnectionStringValue(map, nameof(this.MaxRetries));
            if (string.IsNullOrEmpty(maxRetries))
            {
                this.MaxRetries= Default_MaxRetries;
            }
            else
            {
                var intMaxRetries = Convert.ToInt32(maxRetries);
                if (intMaxRetries > 0)
                {
                    this.RetryInterval = intMaxRetries;
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
            static void AppendIfNotEmpty(StringBuilder sb, string name, string val)
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
