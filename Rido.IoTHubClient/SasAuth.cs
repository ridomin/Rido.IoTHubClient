﻿using System;
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
        internal static string GetUserName(string hostName, string deviceId, string moduleId, string expiryString, string modelId, AuthType auth)
        {
            string username = $"av={apiversion_2021_06_30_preview}&h={hostName}&did={deviceId}&am={auth}";
            if (!string.IsNullOrEmpty(moduleId))
            {
                username += $"&mid={moduleId}";
            }
            if (!string.IsNullOrEmpty(modelId))
            {
                username += $"&dtmi={modelId}";
            }
            if (auth==AuthType.SAS)
            {
                username += $"&se={expiryString}";
            }
            //username += $"&am={auth}";
            return username;
        }
            
        
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
            string username = GetUserName(hostName, deviceId, string.Empty, expiry, modelId, AuthType.SAS);
            byte[] password = CreateSasToken($"{hostName}\n{deviceId}", sasKey, expiry);
            return (username, password);
        }

        internal static (string username, byte[] password) GenerateHubSasCredentials(string hostName, string deviceId, string moduleId, string sasKey, string modelId, int minutes)
        {
            var expiry = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds().ToString();
            string username = GetUserName(hostName, deviceId, moduleId, expiry, modelId, AuthType.SAS);
            byte[] password = CreateSasToken($"{hostName}\n{deviceId}/{moduleId}", sasKey, expiry);
            return (username, password);
        }
    }
}