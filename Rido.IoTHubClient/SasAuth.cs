using System;
using System.Text;

namespace Rido.IoTHubClient
{
    enum AuthType
    {
        SAS,
        X509
    }

    internal class SasAuth
    {
        const string apiversion_2021_06_30_preview = "2021-06-30-preview";
        internal static string GetUserName(string hostName, string deviceId, string expiryString, string modelId, AuthType auth = AuthType.SAS) =>
            $"av={apiversion_2021_06_30_preview}&h={hostName}&did={deviceId}&am={auth}&se={expiryString}&dtmi={modelId}";
        //internal static string GetUserName(string hostName, string deviceId, string moduleId, string expiryString, AuthType auth = AuthType.SAS) =>
        //    $"av={apiversion_2021_06_30_preview}&h={hostName}&did={deviceId}&mid={moduleId}&am={auth}&se={expiryString}";

        internal static string GetUserName(string hostName, string deviceId, string modelId, AuthType auth = AuthType.X509) =>
            $"av={apiversion_2021_06_30_preview}&h={hostName}&did={deviceId}&am={auth}&dtmi={modelId}";

        static byte[] CreateSasToken(string resource, string sasKey, string expiry)
        {
            static byte[] Sign(string requestString, string key)
            {
                using var algorithm = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(key));
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString));
            }
            return Sign($"{resource}\n\n\n{expiry}\n", sasKey);
        }

        internal static string CreateDpsSasToken(string resource, string sasKey, int minutes)
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

        internal static (string username, byte[] password) GenerateHubSasCredentials(string hostName, string deviceId, string sasKey, string modelId, int minutes)
        {
            var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
            string username = GetUserName(hostName, deviceId, expiry, modelId);
            byte[] password = CreateSasToken($"{hostName}\n{deviceId}", sasKey, expiry);
            return (username, password);
        }

        //internal static (string username, byte[] password) GenerateHubSasCredentials(string hostName, string deviceId, string moduleId, string sasKey, int minutes)
        //{
        //    var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
        //    string username = GetUserName(hostName, deviceId, moduleId, expiry);
        //    byte[] password = CreateSasToken($"{hostName}\n{deviceId}/{moduleId}", sasKey, expiry);
        //    return (username, password);
        //}
    }
}