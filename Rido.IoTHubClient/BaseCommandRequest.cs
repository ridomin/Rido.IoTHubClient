namespace Rido.IoTHubClient
{
    public interface IBaseCommandRequest
    {
        public object DeserializeBody(string payload);
    }
}
