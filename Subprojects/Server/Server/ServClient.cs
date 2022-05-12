using SanicballServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanicballCore.Server
{
    public class ServClient
    {
        public Guid Guid { get; }
        public string Name { get; }

        public WebSocketWrapper Connection { get; }

        public bool CurrentlyLoadingStage { get; set; }
        public bool WantsToReturnToLobby { get; set; }

        public ServClient(Guid guid, string name, WebSocketWrapper connection)
        {
            Guid = guid;
            Name = name;
            Connection = connection;
        }
    }
}