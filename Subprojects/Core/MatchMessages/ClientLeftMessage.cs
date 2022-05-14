namespace SanicballCore.MatchMessages
{
    [System.Serializable]
    public class ClientLeftMessage : MatchMessage
    {
        public System.Guid ClientGuid { get; private set; }

        public ClientLeftMessage(System.Guid clientGuid)
        {
            ClientGuid = clientGuid;
        }
    }
}