namespace Rido.IoTHubClient
{
    public class DpsStatus
    {
        public string operationId { get; set; }
        public string status { get; set; }
        public RegistrationState registrationState { get; set; }
    }
}
