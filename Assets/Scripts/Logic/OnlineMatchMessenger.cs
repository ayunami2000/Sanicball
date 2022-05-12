using System;
using System.Collections;
using System.Reflection;
using SanicballCore;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;

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
        public double Timestamp { get; private set; }
        public PlayerMovement Movement { get; private set; }

        public PlayerMovementArgs(double timestamp, PlayerMovement movement)
        {
            Timestamp = timestamp;
            Movement = movement;
        }
    }

    public class OnlineMatchMessenger : MatchMessenger
    {
        public const string APP_ID = "Sanicball";

        private WebSocket client;

        public event EventHandler<PlayerMovementArgs> OnPlayerMovement;
        public event EventHandler<DisconnectArgs> Disconnected;

        public OnlineMatchMessenger(WebSocket client)
        {
            this.client = client;
        }

        public override void SendMessage<T>(T message)
        {
            Stream msg = CerealOne();
            msg.Write(MessageType.MatchMessage);
            msg.Write((float)DateTime.Now);
            CerealTwo(msg, message);
            CerealThree(msg, client);
        }

        public void SendPlayerMovement(MatchPlayer player)
        {
            Stream msg = CerealOne();
            msg.Write(MessageType.MatchMessage);
            msg.Write((float)DateTime.Now);
            PlayerMovement movement = Logic.PlayerMovement.CreateFromPlayer(player);
            movement.WriteToMessage(msg);
            CerealTwo(msg, message);
            CerealThree(msg, client);
        }

        public override void UpdateListeners()
        {
            client.OnMessage += (byte[] message) =>
            {
                Stream msg = UnCerealOne(message);
                switch (msg.ReadByte())
                {
                    case MessageType.MatchMessage:
                        double timestamp = msg.ReadTime(false);
                        MatchMessage message = Newtonsoft.Json.JsonConvert.DeserializeObject<MatchMessage>(msg.ReadString(), serializerSettings);

                        //Use reflection to call ReceiveMessage with the proper type parameter
                        MethodInfo methodToCall = typeof(OnlineMatchMessenger).GetMethod("ReceiveMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                        MethodInfo genericVersion = methodToCall.MakeGenericMethod(message.GetType());
                        genericVersion.Invoke(this, new object[] { message, timestamp });

                        break;

                    case MessageType.PlayerMovementMessage:
                        double time = msg.ReadTime(false);
                        PlayerMovement movement = PlayerMovement.ReadFromMessage(msg);
                        if (OnPlayerMovement != null)
                        {
                            OnPlayerMovement(this, new PlayerMovementArgs(time, movement));
                        }
                        break;
                }
            }
        }

        public override void Close()
        {
            client.Close();
        }

        private void ReceiveMessage<T>(T message, double timestamp) where T : MatchMessage
        {
            float travelTime = (float)(NetTime.Now - timestamp);

            for (int i = 0; i < listeners.Count; i++)
            {
                MatchMessageListener listener = listeners[i];
                if (listener.MessageType == message.GetType())
                {
                    ((MatchMessageHandler<T>)listener.Handler).Invoke(message, travelTime);
                }
            }
        }

        public Stream CerealOne()
        {
            return new MemoryStream();
        }

        public void CerealTwo(Stream message, T thing)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(message, thing);
        }

        public void CerealThree(Stream message, WebSocket ws)
        {
            ws.Send(ReadFully(message));
        }

        public Stream UnCerealOne(byte[] thing)
        {
            return new MemoryStream(thing);
        }

        public T UnCerealTwo(byte[] thing)
        {
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                var dataObj = (T)bf.Deserialize(UnCerealOne(thing));
                return dataObj;
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                Debug.LogError("Failed to parse! Binary converter info: " + ex.Message);
                return null;
            }
        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16*1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}