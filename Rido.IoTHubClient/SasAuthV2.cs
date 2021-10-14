using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{

    enum AuthType
    {
        SAS,
        X509
    }

    internal class SasAuthV2
    {
        internal static string GetUserName(string hostName, string deviceId, string expiryString, AuthType auth = AuthType.SAS) => 
            $"av=2021-06-30-preview&h={hostName}&did={deviceId}&am={auth}&se={expiryString}";
        internal static string GetUserName(string hostName, string deviceId, string moduleId, string expiryString, AuthType auth = AuthType.SAS) =>
            $"av=2021-06-30-preview&h={hostName}&did={deviceId}&mid={moduleId}&am={auth}&se={expiryString}";

        internal static string GetUserName(string hostName, string deviceId, AuthType auth = AuthType.X509) =>
            $"av=2021-06-30-preview&h={hostName}&did={deviceId}&am={auth}";

        static byte[] CreateSasToken(string resource, string sasKey, string expiry)
        {
            static byte[] Sign(string requestString, string key)
            {
                using var algorithm = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(key));
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString));
            }
            return Sign($"{resource}\n\n\n{expiry}\n", sasKey);
            //return $"SharedAccessSignature sr={resource}&sig={sig}&se={expiry}";
        }

        internal static (string username, byte[] password) GenerateHubSasCredentials(string hostName, string deviceId, string sasKey, int minutes)
        {
            var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
            string username = GetUserName(hostName, deviceId, expiry);
            byte[] password = CreateSasToken($"{hostName}\n{deviceId}", sasKey, expiry);
            return (username, password);
        }

        internal static (string username, byte[] password) GenerateHubSasCredentials(string hostName, string deviceId, string moduleId, string sasKey, int minutes)
        {
            var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
            string username = GetUserName(hostName, deviceId, moduleId, expiry);
            byte[] password = CreateSasToken($"{hostName}\n{deviceId}/{moduleId}", sasKey, expiry);
            return (username, password);
        }
    }
}
