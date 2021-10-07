using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Rido.IoTHubClient
{
    public class DeviceConnectionString
    {
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get;  set; }
        public string Auth { get; set; } = "SAS";
        public string CertPath { get; private set; }
        public string CertPassword { get; private set; }


        public DeviceConnectionString(){}
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
            this.Auth = GetConnectionStringValue(map,nameof(this.Auth));
        }

        public override string ToString()
        {
            return $"HostName={HostName};DeviceId={DeviceId};SharedAccessKey={SharedAccessKey};Auth={Auth}";
        }

        public string GetUserName2(string expiryString)
        {
            string username = $"av=2021-06-30-preview&" +
                   $"h={this.HostName}&" +
                   $"did={this.DeviceId}&" +
                   $"am=SAS&" +
                   $"se={expiryString}";

            return username;
        }

        public byte[] BuildSasToken2(string expiryString)
        {
            var algorithm = new HMACSHA256(Convert.FromBase64String(this.SharedAccessKey));
            string toSign = $"{this.HostName}\n{this.DeviceId}\n\n\n{expiryString}\n";
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(toSign));
        }
    }
}
