using SanicballCore;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Sanicball.Logic
{
    public class DisconnectArgs : EventArgs
    {
        public string Reason { get; private set; }

        public DisconnectArgs(string reason)
        {
            Reason = reason;
        }
    }

    public class PlayerMovementArgs : EventArgs
    {
        public long Timestamp { get; private set; }
        public PlayerMovement Movement { get; private set; }

        public PlayerMovementArgs(long timestamp, PlayerMovement movement)
        {
            Timestamp = timestamp;
            Movement = movement;
        }
    }

    public class OnlineMatchMessenger : MatchMessenger
    {
        public const string APP_ID = "Sanicball";

        private WebSocket client;

        //Settings to use for both serializing and deserializing messages
        private Newtonsoft.Json.JsonSerializerSettings serializerSettings;

        public event EventHandler<PlayerMovementArgs> OnPlayerMovement;
        public event EventHandler<DisconnectArgs> Disconnected;

        public OnlineMatchMessenger(WebSocket client)
        {
            this.client = client;

            serializerSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
            };
        }

        public override void SendMessage<T>(T message)
        {
            var net = new MessageWrapper(MessageTypes.Match);
            net.Writer.Write(DateTime.Now.Ticks);

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(message, serializerSettings);
            net.Writer.Write(data);

            client.Send(net.GetBytes());
            net.Dispose();
        }

        public void SendPlayerMovement(MatchPlayer player)
        {
            var msg = new MessageWrapper(MessageTypes.PlayerMovement);
            msg.Writer.Write(DateTime.Now.Ticks);

            var movement = Logic.PlayerMovement.CreateFromPlayer(player);
            movement.WriteToMessage(msg.Writer);
            client.Send(msg.GetBytes());
        }

        public override void UpdateListeners()
        {
            if (client.error != null)
            {
                Disconnected?.Invoke(this, new DisconnectArgs($"You were disconnected from the server. {client.error}"));
            }

            byte[] msg;
            while ((msg = client.Recv()) != null)
            {
                using (var message = new MessageWrapper(msg))
                {
                    switch (message.Type)
                    {
                        case MessageTypes.Disconnect:
                            Disconnected?.Invoke(this, new DisconnectArgs(message.Reader.ReadString()));
                            break;

                        case MessageTypes.Match:
                            var timestamp = message.Reader.ReadInt64();
                            var value = message.Reader.ReadString();

                            Debug.Log(value);

                            var matchMessage = Newtonsoft.Json.JsonConvert.DeserializeObject<MatchMessage>(value, serializerSettings);

                            Debug.Log(matchMessage.GetType());

                            //Use reflection to call ReceiveMessage with the proper type parameter
                            var methodToCall = typeof(OnlineMatchMessenger).GetMethod("ReceiveMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                            var genericVersion = methodToCall.MakeGenericMethod(matchMessage.GetType());
                            genericVersion.Invoke(this, new object[] { matchMessage, timestamp });

                            break;

                        case MessageTypes.PlayerMovement:
                            timestamp = message.Reader.ReadInt64();
                            var movement = PlayerMovement.ReadFromMessage(message.Reader);
                            OnPlayerMovement?.Invoke(this, new PlayerMovementArgs(timestamp, movement));
                            break;

                        default:
                            Debug.Log("Received unhandled message of type " + message.Type);
                            break;
                    }
                }
            }
        }

        public override void Close()
        {
            using (var message = new MessageWrapper(MessageTypes.Disconnect))
            {
                message.Writer.Write("Client disconnecting");
                client.Send(message.GetBytes());
                client.Close();
            }
        }

        private void ReceiveMessage<T>(T message, long timestamp) where T : SanicballCore.MatchMessage
        {
            var travelTime = (float)(DateTime.Now.Ticks - timestamp) / TimeSpan.TicksPerSecond;

            for (var i = 0; i < listeners.Count; i++)
            {
                var listener = listeners[i];
                if (listener.MessageType == message.GetType())
                {
                    ((MatchMessageHandler<T>)listener.Handler).Invoke(message, travelTime);
                }
            }
        }
    }
}