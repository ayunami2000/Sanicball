using System;
using System.Collections.Generic;
using System.Net;
using Sanicball.Data;
using Sanicball.Logic;
using UnityEngine;
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

        //Stores server browser IPs, so they can be differentiated from LAN servers
        private List<string> serverBrowserIPs = new List<string>();

        private WWW serverBrowserRequester;

        public void RefreshServers()
        {
            serverBrowserIPs.Clear();

			serverBrowserRequester = new WWW(ActiveData.GameSettings.serverListURL);

            serverCountField.text = "Refreshing servers, hang on...";
            errorField.enabled = false;

            //Clear old servers
            foreach (var serv in servers)
            {
                Destroy(serv.gameObject);
            }
            servers.Clear();
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
                RefreshServers();
            }

            //Check for response from the server browser requester
            if (serverBrowserRequester != null && serverBrowserRequester.isDone)
            {
                if (string.IsNullOrEmpty(serverBrowserRequester.error))
                {
                    string result = serverBrowserRequester.text;
                    string[] entries = result.Split(new string[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string entry in entries)
                    {
                        WWW discoveryClient = new WWW("http" + entry.Remove(0,2));
                        System.Threading.Thread discoverThread = new System.Threading.Thread(() => {
                            while (discoveryClient != null && discoveryClient.isDone) {}
                            if (string.IsNullOrEmpty(discoveryClient.error))
                            {
                                string serverResult = discoveryClient.text;
                                string[] serverInfo = serverResult.Split(new string[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries);

                                var server = Instantiate(serverListItemPrefab);
                                server.transform.SetParent(targetServerListContainer, false);
                                server.Init(entry, serverInfo[0], bool.Parse(serverInfo[1]), int.Parse(serverInfo[2]), int.Parse(serverInfo[3]));
                                servers.Add(server);
                                RefreshNavigation();

                                serverCountField.text = servers.Count + (servers.Count == 1 ? " server" : " servers");
                            }
                        });
                        serverBrowserIPs.Add(entry);
                    }
					serverCountField.text = "0 servers";
                }
                else
                {
                    Debug.LogError("Failed to receive servers - " + serverBrowserRequester.error);
					serverCountField.text = "Cannot access server list URL!";
                }

                serverBrowserRequester = null;
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