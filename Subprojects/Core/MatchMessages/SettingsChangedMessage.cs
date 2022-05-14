namespace SanicballCore.MatchMessages
{
    [System.Serializable]
    public class SettingsChangedMessage : MatchMessage
    {
        public MatchSettings NewMatchSettings { get; private set; }

        public SettingsChangedMessage(MatchSettings newMatchSettings)
        {
            NewMatchSettings = newMatchSettings;
        }
    }
}