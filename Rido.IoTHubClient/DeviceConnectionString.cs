using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Rido.IoTHubClient
{
    public class DeviceConnectionString
    {
        public string HostName { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }

        public DeviceConnectionString()
        {

        }
        public DeviceConnectionString(string cs)
        {
            ParseConnectionString(cs);
        }

        private void ParseConnectionString(string cs)
        {
            string GetConnectionStringValue(IDictionary<string, string> dict, string propertyName)
            {
                if (!dict.TryGetValue(propertyName, out string value))
                {
                    Console.WriteLine($"The connection string is missing the property: {propertyName}");
                }
                else
                {
                    // Console.WriteLine($"Connection Property Found: {propertyName}={value}");
                }
                return value;
            }

            IDictionary<string, string> map = cs.ToDictionary(';', '=');
            this.HostName = GetConnectionStringValue(map, nameof(this.HostName));
            this.DeviceId = GetConnectionStringValue(map, nameof(this.DeviceId));
            this.SharedAccessKey = GetConnectionStringValue(map, nameof(this.SharedAccessKey));
        }

        public override string ToString()
        {
            return $"HostName={HostName};DeviceId={DeviceId};SharedAccessKey={SharedAccessKey}";
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
