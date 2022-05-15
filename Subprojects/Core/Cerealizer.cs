using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using SanicballCore.MatchMessages;

namespace SanicballCore
{
    public class Cerealizer
    {
        public class KnownTypesBinder : System.Runtime.Serialization.SerializationBinder
        {
            private static List<Type> KnownTypes = new List<Type> {
                typeof(Guid),
                typeof(ClientInfo),
                typeof(MatchSettings),
                typeof(MatchState),
                typeof(ControlType),
                typeof(AISkillLevel),
                typeof(StageRotationMode),
                typeof(AllowedTiers),
                typeof(TierRotationMode),
                typeof(ChatMessageType),

                typeof(AutoStartTimerMessage),
                typeof(ChangedReadyMessage),
                typeof(CharacterChangedMessage),
                typeof(ChatMessage),
                typeof(CheckpointPassedMessage),
                typeof(ClientJoinedMessage),
                typeof(ClientLeftMessage),
                typeof(DoneRacingMessage),
                typeof(LoadLobbyMessage),
                typeof(LoadRaceMessage),
                typeof(PlayerJoinedMessage),
                typeof(PlayerLeftMessage),
                typeof(RaceFinishedMessage),
                typeof(RaceTimeoutMessage),
                typeof(SettingsChangedMessage),
                typeof(StartRaceMessage),

                typeof(List<MatchClientState>),
                typeof(List<MatchPlayerState>),
                typeof(MatchClientState),
                typeof(MatchPlayerState)
            };

            public override Type BindToType(string assemblyName, string typeName)
            {
                IEnumerable<Type> fard = KnownTypes.Where(t => t.FullName.Equals(typeName));
                /*
                string fff = fard.DefaultIfEmpty(typeof(Object)).First().FullName;
                Console.WriteLine(typeName);
                Console.WriteLine(fff);
                try
                {
                    UnityEngine.Debug.Log(typeName);
                    UnityEngine.Debug.Log(fff);
                }
                catch (System.Security.SecurityException e)
                {
                    //
                }
                */
                return fard.Any() ? null : typeof(Object);
            }
        }

        public static byte[] ReadAllBytes(BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }
        }

        public static byte[] Cereal<T>(T thing)
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Binder = new KnownTypesBinder();
            using (var ms = new MemoryStream())
            {
                try
                {
                    bf.Serialize(ms, thing);
                    return ms.ToArray();
                }
                catch (System.Runtime.Serialization.SerializationException ex)
                {
                    return null;
                }
            }
        }

        public static T UnCereal<T>(byte[] thing)
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                binForm.Binder = new KnownTypesBinder();
                memStream.Write(thing, 0, thing.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                try
                {
                    var obj = binForm.Deserialize(memStream);
                    return (T)obj;
                }
                catch (System.Runtime.Serialization.SerializationException ex)
                {
                    return (T)new Object();
                }
            }
        }
    }
}
