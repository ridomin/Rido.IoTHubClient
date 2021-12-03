using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public class IoTHubHttpClient
    {
        static string js(object o) => System.Text.Json.JsonSerializer.Serialize(o);

        ConnectionSettings cs;
        public IoTHubHttpClient(ConnectionSettings settings)
        {
            cs = settings;
        }
        public async Task<HttpResponseMessage> SendTelemetryAsync(object payload)
        {
            string serializedPayload = string.Empty;
            if (payload is string)
            {
                serializedPayload = payload as string;
            }
            else
            {
                serializedPayload = js(payload);
                ;
            }


            if (cs.Auth == "SAS")
            {
                string token = string.Empty;
                string urlTelemetry = $"https://{cs.HostName}/devices/{cs.DeviceId}/";

                if (string.IsNullOrEmpty(cs.ModuleId))
                {
                    (_, token) = SasAuth.GenerateHubSasCredentials(cs.HostName, cs.DeviceId, cs.SharedAccessKey, "", cs.SasMinutes);
                }
                else
                {
                    throw new NotImplementedException("Http Telemetry not implemented for modules");
                    //urlTelemetry += $"modules/{cs.ModuleId}";
                    //(_, token) = SasAuth.GenerateHubSasCredentials(cs.HostName,  $"{cs.DeviceId}/{cs.ModuleId}", cs.SharedAccessKey, "", cs.SasMinutes);
                } 

                urlTelemetry += "/messages/events?api-version=2020-03-13";

                return await new HttpClient()
                            .SendAsync(
                                new HttpRequestMessage(
                                    HttpMethod.Post,
                                    urlTelemetry)
                                {
                                    Headers = { { "authorization", token } },
                                    Content = new StringContent(
                                            serializedPayload,
                                            System.Text.Encoding.UTF8,
                                            "application/json")
                                });
            }
            else if (cs.Auth == "X509")
            {
                var segments = cs.X509Key.Split('|');
                string pfxpath = segments[0];
                string pfxpwd = segments[1];
                X509Certificate2 cert = new X509Certificate2(pfxpath, pfxpwd);
                var deviceId = cert.Subject[3..];

                string urlTelemetry = $"https://{cs.HostName}/devices/{deviceId}/messages/events?api-version=2020-03-13";

                var handler = new HttpClientHandler();
                handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                handler.ClientCertificates.Add(cert);

                return await new HttpClient(handler)
                           .SendAsync(
                               new HttpRequestMessage(
                                   HttpMethod.Post,
                                   urlTelemetry)
                               {
                                   Content = new StringContent(
                                           serializedPayload,
                                           System.Text.Encoding.UTF8,
                                           "application/json")
                               });
            }
            else
            {
                throw new NotImplementedException("Http Telemetry not implemented with auth: " + cs.Auth);
            }
        }
    }
}
