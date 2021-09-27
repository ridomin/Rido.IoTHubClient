using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Rido.IoTHubClient
{
    public class SharedAccessSignatureBuilder
    {
        private string _key;

        public SharedAccessSignatureBuilder()
        {
            TimeToLive = TimeSpan.FromMinutes(60);
        }

        public string KeyName { get; set; }

        public string Key
        {
            get => _key;

            set
            {
                _key = value;
            }
        }

        public string Target { get; set; }

        public TimeSpan TimeToLive { get; set; }

        public string ToSignature()
        {
            return BuildSignature(KeyName, Key, Target, TimeToLive);
        }

        private string BuildSignature(string keyName, string key, string target, TimeSpan timeToLive)
        {
            string expiresOn = BuildExpiresOn(timeToLive);
            string audience = WebUtility.UrlEncode(target);

            var fields = new List<string>
            {
                audience,
                expiresOn,
            };

            // Example string to be signed:
            // dh://myiothub.azure-devices.net/a/b/c?myvalue1=a
            // <Value for ExpiresOn>

            string signature = Sign(string.Join("\n", fields), key);

            // Example returned string:
            // SharedAccessSignature sr=ENCODED(dh://myiothub.azure-devices.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>[&skn=<KeyName>]

            var buffer = new StringBuilder();
            buffer.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1}={2}&{3}={4}&{5}={6}",
                "SharedAccessSignature",
                "sr", audience,
                "sig", WebUtility.UrlEncode(signature),
                "se", WebUtility.UrlEncode(expiresOn));

            if (!String.IsNullOrWhiteSpace(keyName))
            {
                buffer.AppendFormat(CultureInfo.InvariantCulture, "&{0}={1}",
                    "skn", WebUtility.UrlEncode(keyName));
            }

            return buffer.ToString();
        }

        private string BuildExpiresOn(TimeSpan timeToLive)
        {
            DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime expiresOn = DateTime.UtcNow.Add(timeToLive);
            TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
            long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
            return Convert.ToString(seconds, CultureInfo.InvariantCulture);
        }

        protected virtual string Sign(string requestString, string key)
        {
            using (var algorithm = new HMACSHA256(Convert.FromBase64String(key)))
            {
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
            }
        }
    }
}
