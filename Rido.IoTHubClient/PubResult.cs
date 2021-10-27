namespace Rido.IoTHubClient
{
    public enum PubResult
    {
        Success = 0,
        NoMatchingSubscribers = 0x10,
        UnspecifiedError = 0x80,
        ImplementationSpecificError = 131,
        NotAuthorized = 135,
        TopicNameInvalid = 144,
        PacketIdentifierInUse = 145,
        QuotaExceeded = 151,
        PayloadFormatInvalid = 153
    }

}