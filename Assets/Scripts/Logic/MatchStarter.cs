using SanicballCore;
using UnityEngine;

namespace Sanicball.Logic
{
    public class MatchStarter : MonoBehaviour
    {
        public const string APP_ID = "Sanicball";

        [SerializeField]
        private MatchManager matchManagerPrefab = null;
        [SerializeField]
        private UI.Popup connectingPopupPrefab = null;
        [SerializeField]
        private UI.PopupHandler popupHandler = null;

        private UI.PopupConnecting activeConnectingPopup;

        //NetClient for when joining online matches
        private WebSocket joiningClient;

        private void Update()
        {

        }

        public void BeginLocalGame()
        {
            MatchManager manager = Instantiate(matchManagerPrefab);
            manager.InitLocalMatch();
        }

        public void JoinOnlineGame(string url = "ws://sus.shhnowisnottheti.me:25069")
        {
            joiningClient = WebSocketFactory.CreateInstance(url);

            popupHandler.OpenPopup(connectingPopupPrefab);

            activeConnectingPopup = FindObjectOfType<UI.PopupConnecting>();
            ws.OnOpen += () =>
            {
                //Create approval message
                ClientInfo info = new ClientInfo(GameVersion.AS_FLOAT, GameVersion.IS_TESTING);
                ws.Send(OnlineMatchMessenger.Cereal(info, MessageType.MatchMessage));

                Debug.Log("Connected! Now waiting for match state");
                activeConnectingPopup.ShowMessage("Receiving match state...");
            };
            ws.OnMessage += (byte[] msg) =>
            {
                byte type = msg.ReadByte();
                if (type == MessageType.InitMessage)
                {
                    try
                    {
                        MatchState matchInfo = MatchState.ReadFromMessage(msg);
                        BeginOnlineGame(matchInfo);
                    }
                    catch (System.Exception ex)
                    {
                        activeConnectingPopup.ShowMessage("Failed to read match message - cannot join server!");
                        Debug.LogError("Could not read match state, error: " + ex.Message);
                    }
                }
            };
            ws.OnClose += (WebSocketCloseCode code) =>
            {
                activeConnectingPopup.ShowMessage(code.ToString());
            };
            ws.Connect();
        }

        //Called when succesfully connected to a server
        private void BeginOnlineGame(MatchState matchState)
        {
            MatchManager manager = Instantiate(matchManagerPrefab);
            manager.InitOnlineMatch(joiningClient, matchState);
        }
    }
}