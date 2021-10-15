using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace v1client
{
    class Program
    {
        private static readonly ConsoleEventListener _listener = new ConsoleEventListener();

        static Program()
        {

        }

        static DeviceClient ConnectWithCert(string hostname, string certpath, string certpwd)
        {
            var cert = new X509Certificate2(certpath, certpwd);
            var security = new SecurityProviderX509Certificate(cert);
            var cid = cert.SubjectName.Name.Substring(3);


            var dc = DeviceClient.Create(
                hostname,
                new DeviceAuthenticationWithX509Certificate(cid, security.GetAuthenticationCertificate()),
                TransportType.Mqtt);
            return dc;
        }

        static async Task Main(string[] args)
        {
            var dc = ConnectWithCert("broker.azure-devices.net",
                                 "../../../../.certs/devx1.pfx", "1234");
            //var cs = Environment.GetEnvironmentVariable("CS");
            //var dc = DeviceClient.CreateFromConnectionString(cs, TransportType.Mqtt);
            await dc.OpenAsync();
            var twin = await dc.GetTwinAsync();
            Console.WriteLine(twin.ToJson(Newtonsoft.Json.Formatting.Indented));

            //dc.SetConnectionStatusChangesHandler((s, r) =>
            //{
            //    Console.WriteLine(s + r.ToString());
            //});

            //await dc.SetDesiredPropertyUpdateCallbackAsync((a, b) => 
            //{
            //    Console.WriteLine(a.ToJson(Newtonsoft.Json.Formatting.Indented));
            //    return Task.FromResult(0);
            //}, null);

            Console.ReadLine();
        }
    }
}
