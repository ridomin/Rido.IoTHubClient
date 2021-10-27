using Rido.IoTHubClient;
using System;
using System.Text;
using uPLibrary.Networking.M2Mqtt;

namespace m2m_sample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var conn = new ConnectionSettings
            {
                HostName = "broker.azure-devices.net",
                DeviceId = "d4",
                SharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.Empty.ToString("N")))
            };

            MqttClient client = new MqttClient(conn.HostName, 8883,true, MqttSslProtocols.TLSv1_2, null, null);
            (string username, string password) = SasAuth.GenerateHubSasCredentials(
                conn.HostName, 
                conn.DeviceId, 
                conn.SharedAccessKey, 
                "dtmi:rido:m2m;22", 
                conn.SasMinutes);

            client.Connect(conn.DeviceId, username, password);
            Console.WriteLine(client.IsConnected);
            client.MqttMsgPublishReceived += (o, e) =>
            {
                
            };
        }
    }
}
