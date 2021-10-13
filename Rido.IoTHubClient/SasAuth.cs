using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    internal class SasAuth
    {
        internal static string GetUserName(string hostName, string deviceId) => $"{hostName}/{deviceId}/?api-version=2020-05-31-preview";
        internal static string CreateSasToken(string resource, string sasKey, int minutes)
        {
            static string Sign(string requestString, string key)
            {
                using var algorithm = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(key));
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
            }
            var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
            var sig = System.Net.WebUtility.UrlEncode(Sign($"{resource}\n{expiry}", sasKey));
            return $"SharedAccessSignature sr={resource}&sig={sig}&se={expiry}";
        }

        internal static (string username, string password) GenerateHubSasCredentials(string hostName, string deviceId, string sasKey, int minutes = 60) => 
            (GetUserName(hostName, deviceId), CreateSasToken($"{hostName}/devices/{deviceId}", sasKey, minutes));
    }
}
