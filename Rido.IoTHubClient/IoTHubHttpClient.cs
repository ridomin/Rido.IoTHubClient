using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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
            (_, string token) = SasAuth.GenerateHubSasCredentials(cs.HostName, cs.DeviceId, cs.SharedAccessKey, "", cs.SasMinutes);
            string urlTelemetry = $"https://{cs.HostName}/devices/{cs.DeviceId}/messages/events?api-version=2020-03-13";
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
    }
}
