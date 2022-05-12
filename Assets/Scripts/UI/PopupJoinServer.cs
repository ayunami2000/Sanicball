using Sanicball.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace Sanicball.UI
{
    public class PopupJoinServer : MonoBehaviour
    {
        [SerializeField]
        private InputField ipInput;
        [SerializeField]
        private Text portOutput;

        private const int LOWEST_PORT_NUM = 1024;
        private const int HIGHEST_PORT_NUM = 49151;

        public void Connect()
        {
            portOutput.text = "";
            
            if (System.Uri.TryCreate(ipInput.text, System.UriKind.Absolute, out var uri))
            {
                var matchStarter = FindObjectOfType<MatchStarter>();
                StartCoroutine(matchStarter.JoinOnlineGame(uri));
            }
            else
            {
                portOutput.text = "URL must be valid!";
            }
        }
    }
}
