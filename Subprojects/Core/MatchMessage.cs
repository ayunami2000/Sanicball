namespace SanicballCore
{
    public delegate void MatchMessageHandler<T>(T message, float travelTime) where T : MatchMessage;

    [System.Serializable]
    public abstract class MatchMessage
    {
    }
}