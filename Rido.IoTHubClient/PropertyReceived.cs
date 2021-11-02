namespace Rido.IoTHubClient
{
    public class PropertyReceived
    {
        public string Rid { get; set; }
        public string PropertyMessageJson { get; set; }
        public string Topic { get; set; }
        public int Version { get; set; }

    }
}
