using Newtonsoft.Json;
using Sanicball.Data;
using SanicballCore;
using System;
using System.Collections;
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

        internal void JoinOnlineGame(Guid id)
        {
            var baseUri = new Uri(ActiveData.GameSettings.serverListURL);
            var uri = new UriBuilder(new Uri(baseUri, id.ToString())) { Scheme = baseUri.Scheme == "https" ? "wss" : "ws" };

            StartCoroutine(JoinOnlineGame(uri.Uri));
        }

        public void BeginLocalGame()
        {
            var manager = Instantiate(matchManagerPrefab);
            manager.InitLocalMatch();
        }

        public IEnumerator JoinOnlineGame(Uri endpoint)
        {
            joiningClient = new WebSocket(endpoint);

            popupHandler.OpenPopup(connectingPopupPrefab);
            activeConnectingPopup = FindObjectOfType<UI.PopupConnecting>();

            yield return StartCoroutine(joiningClient.Connect());

            if (joiningClient.error != null)
            {
                activeConnectingPopup.ShowMessage($"Failed to join! {joiningClient.error}");
            }
            else
            {
                using (var newMessage = new MessageWrapper(MessageTypes.Connect))
                {
                    var info = new ClientInfo(GameVersion.AS_FLOAT, GameVersion.IS_TESTING);
                    newMessage.Writer.Write(JsonConvert.SerializeObject(info));
                    var buffer = newMessage.GetBytes();
                    joiningClient.Send(buffer);
                }

                var done = false;
                byte[] msg;
                while (!done && joiningClient != null)
                {
                    msg = joiningClient.Recv();
                    if (msg != null)
                    {
                        using (var message = new MessageWrapper(msg))
                        {
                            switch (message.Type)
                            {                                
                                case MessageTypes.Validate: // should only be recieved if validation fails

                                    var valid = message.Reader.ReadBoolean();
                                    if (!valid)
                                    {
                                        var str = message.Reader.ReadString();
                                        activeConnectingPopup.ShowMessage($"Failed to join! {str}");
                                        joiningClient.Close();
                                        joiningClient = null;
                                    }

                                    break;

                                case MessageTypes.Connect:
                                    Debug.Log("Connected! Now waiting for match state");
                                    activeConnectingPopup.ShowMessage("Receiving match state...");

                                    try
                                    {
                                        var str = message.Reader.ReadString();
                                        var matchInfo = JsonConvert.DeserializeObject<MatchState>(str);
                                        done = true;
                                        BeginOnlineGame(matchInfo);
                                    }
                                    catch (Exception ex)
                                    {
                                        activeConnectingPopup.ShowMessage("Failed to read match message - cannot join server!");
                                        Debug.LogError("Could not read match state, error: " + ex.Message);
                                    }

                                    break;

                                case MessageTypes.Disconnect:
                                    activeConnectingPopup.ShowMessage(message.Reader.ReadString());
                                    break;
                            }
                        }
                    }


                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        popupHandler.CloseActivePopup();
                        joiningClient.Close();
                        joiningClient = null;
                    }

                    yield return null;
                }
            }
        }

        //Called when succesfully connected to a server
        private void BeginOnlineGame(MatchState matchState)
        {
            var manager = Instantiate(matchManagerPrefab);
            manager.InitOnlineMatch(joiningClient, matchState);
        }
    }
}