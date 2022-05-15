using Sanicball.Logic;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sanicball.UI
{
    public class ServerListItem : MonoBehaviour
    {
        [SerializeField]
        private Text serverNameText = null;
        [SerializeField]
        private Text serverStatusText = null;
        [SerializeField]
        private Text playerCountText = null;
        [SerializeField]
        private Text pingText = null;

        public string Id { get; private set; }

        public void Init(string id, string name, bool inRace, int players, int maxPlayers, long pingTime)
        {
            Id = id;

            serverNameText.text = name;
            serverStatusText.text = inRace ? "In race" : "In lobby";
            playerCountText.text = players + "/" + maxPlayers;
            pingText.text = ((DateTime.UtcNow.Ticks - pingTime) / TimeSpan.TicksPerMillisecond).ToString() + "ms";
        }

        public void Join()
        {
            MatchStarter starter = FindObjectOfType<MatchStarter>();
            if (starter)
            {
                StartCoroutine(starter.JoinOnlineGame(new Uri(Id, UriKind.Absolute)));
            }
            else
            {
                Debug.LogError("No match starter found");
            }
        }
    }
}
