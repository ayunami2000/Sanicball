using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using Sanicball.Data;
using Sanicball.Logic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Sanicball.UI
{
    public class OnlinePanel : MonoBehaviour
    {
        public Transform targetServerListContainer;
        public Text errorField;
        public Text serverCountField;
        public ServerListItem serverListItemPrefab;
        public Selectable aboveList;
        public Selectable belowList;

        private List<ServerListItem> servers = new List<ServerListItem>();

        private UnityWebRequest serverBrowserRequester;
        private DateTime latestLocalRefreshTime;
        private DateTime latestBrowserRefreshTime;

        public void RefreshServers()
        {
            StartCoroutine(RefreshServerRoutine());
        }

        public IEnumerator RefreshServerRoutine()
        {
            latestLocalRefreshTime = DateTime.Now;

            serverCountField.text = "Refreshing servers, hang on...";
            errorField.enabled = false;

            //Clear old servers
            foreach (var serv in servers)
            {
                Destroy(serv.gameObject);
            }
            servers.Clear();
            
			serverBrowserRequester = UnityWebRequest.Get(ActiveData.GameSettings.serverListURL);
            yield return serverBrowserRequester.SendWebRequest();

            if(serverBrowserRequester.responseCode == 200)
            {
                try
                {
                    var results = JArray.Parse(serverBrowserRequester.downloadHandler.text);

                    foreach (var result in results)
                    {
                        var server = Instantiate(serverListItemPrefab);
                        server.transform.SetParent(targetServerListContainer, false);

                        var id = Guid.Parse(result["id"].ToString());
                        var name = result["name"].ToString();
                        var inRace = result["inGame"].ToObject<bool>();
                        var players = result["currentPlayers"].ToObject<int>();
                        var maxPlayers = result["maxPlayers"].ToObject<int>();

                        server.Init(id, name, inRace, players, maxPlayers);
                        servers.Add(server);
                        RefreshNavigation();
                    }

                    serverCountField.text = results.Count + (results.Count == 1 ? " server" : " servers");
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to receive servers - " + ex.Message);
                    serverCountField.text = "Cannot access server list URL!";
                }
            }
            else
            {
                Debug.LogError("Failed to receive servers - " + serverBrowserRequester.error);
                serverCountField.text = "Cannot access server list URL!";
            }
        }

        private void Awake()
        {
            errorField.enabled = false;
        }

        private void Update()
        {
            //Refresh on f5 (pretty nifty)
            if (Input.GetKeyDown(KeyCode.F5))
            {
                StartCoroutine(RefreshServerRoutine());
            }           
        }

        private void RefreshNavigation()
        {
            for (var i = 0; i < servers.Count; i++)
            {
                var button = servers[i].GetComponent<Button>();
                if (button)
                {
                    var nav = new Navigation() { mode = Navigation.Mode.Explicit };
                    //Up navigation
                    if (i == 0)
                    {
                        nav.selectOnUp = aboveList;
                        var nav2 = aboveList.navigation;
                        nav2.selectOnDown = button;
                        aboveList.navigation = nav2;
                    }
                    else
                    {
                        nav.selectOnUp = servers[i - 1].GetComponent<Button>();
                    }
                    //Down navigation
                    if (i == servers.Count - 1)
                    {
                        nav.selectOnDown = belowList;
                        var nav2 = belowList.navigation;
                        nav2.selectOnUp = button;
                        belowList.navigation = nav2;
                    }
                    else
                    {
                        nav.selectOnDown = servers[i + 1].GetComponent<Button>();
                    }

                    button.navigation = nav;
                }
            }
        }
    }
}
