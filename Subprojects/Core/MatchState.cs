using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SanicballCore
{
    [System.Serializable]
    public class MatchState
    {
        public List<MatchClientState> Clients { get; private set; }
        public List<MatchPlayerState> Players { get; private set; }
        public MatchSettings Settings { get; private set; }
        public bool InRace { get; private set; }
        public float CurAutoStartTime { get; private set; }

        public MatchState(List<MatchClientState> clients, List<MatchPlayerState> players, MatchSettings settings, bool inRace, float curAutoStartTime)
        {
            Clients = clients;
            Players = players;
            Settings = settings;
            InRace = inRace;
            CurAutoStartTime = curAutoStartTime;
        }

        public byte[] WriteToMessage()
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output, Encoding.UTF8, true);

            //Client count
            writer.Write(Clients.Count);                   //Int32
            //Clients
            for (int i = 0; i < Clients.Count; i++)
            {
                MatchClientState c = Clients[i];
                //Write guid (as byte array)
                writer.Write(c.Guid);                      //Byte array w size
                //Write name
                writer.Write(c.Name);                      //String
            }
            //Player count
            writer.Write(Players.Count);
            //Players
            for (int i = 0; i < Players.Count; i++)
            {
                MatchPlayerState p = Players[i];
                //Client GUID (as byte array)
                writer.Write(p.ClientGuid);                //Byte array w size
                //Control type enum as int
                writer.Write((int)p.CtrlType);             //Int32 (Cast to ControlType)
                //Ready to race bool
                writer.Write(p.ReadyToRace);               //Bool
                //Character id
                writer.Write(p.CharacterId);               //Int32
            }
            //Match settings properties, written in the order they appear in code
            writer.Write(Settings.StageId);                //Int32
            writer.Write(Settings.Laps);                   //Int32
            writer.Write(Settings.AICount);                //Int32
            writer.Write((int)Settings.AISkill);           //Int32 (Cast to AISkillLevel)
            writer.Write(Settings.AutoStartTime);          //Int32
            writer.Write(Settings.AutoStartMinPlayers);    //Int32
            writer.Write(Settings.AutoReturnTime);         //Int32
            writer.Write(Settings.VoteRatio);              //Float
            writer.Write((int)Settings.StageRotationMode); //Int32 (Cast to StageRotationMode)

            //In race
            writer.Write(InRace);
            //Cur auto start time
            writer.Write(CurAutoStartTime);

            return output.ToArray();
        }

        public static MatchState ReadFromMessage(BinaryReader reader)
        {
            //Clients
            int clientCount = reader.ReadInt32();
            List<MatchClientState> clients = new List<MatchClientState>();
            for (int i = 0; i < clientCount; i++)
            {
                System.Guid guid = reader.ReadGuid();
                string name = reader.ReadString();

                clients.Add(new MatchClientState(guid, name));
            }
            //Players
            int playerCount = reader.ReadInt32();
            List<MatchPlayerState> players = new List<MatchPlayerState>();
            for (int i = 0; i < playerCount; i++)
            {
                System.Guid clientGuid = reader.ReadGuid();
                ControlType ctrlType = (ControlType)reader.ReadInt32();
                bool readyToRace = reader.ReadBoolean();
                int characterId = reader.ReadInt32();

                players.Add(new MatchPlayerState(clientGuid, ctrlType, readyToRace, characterId));
            }

            //Match settings
            MatchSettings settings = new MatchSettings()
            {
                StageId = reader.ReadInt32(),
                Laps = reader.ReadInt32(),
                AICount = reader.ReadInt32(),
                AISkill = (AISkillLevel)reader.ReadInt32(),
                AutoStartTime = reader.ReadInt32(),
                AutoStartMinPlayers = reader.ReadInt32(),
                AutoReturnTime = reader.ReadInt32(),
                VoteRatio = reader.ReadSingle(),
                StageRotationMode = (StageRotationMode)reader.ReadInt32()
            };
            bool inRace = reader.ReadBoolean();
            float curAutoStartTime = reader.ReadSingle();

            return new MatchState(clients, players, settings, inRace, curAutoStartTime);

        }
    }

    [System.Serializable]
    public class MatchClientState
    {
        public System.Guid Guid { get; private set; }
        public string Name { get; private set; }

        public MatchClientState(System.Guid guid, string name)
        {
            Guid = guid;
            Name = name;
        }
    }

    [System.Serializable]
    public class MatchPlayerState
    {
        public System.Guid ClientGuid { get; private set; }
        public ControlType CtrlType { get; private set; }
        public bool ReadyToRace { get; private set; }
        public int CharacterId { get; private set; }

        public MatchPlayerState(System.Guid clientGuid, ControlType ctrlType, bool readyToRace, int characterId)
        {
            ClientGuid = clientGuid;
            CtrlType = ctrlType;
            ReadyToRace = readyToRace;
            CharacterId = characterId;
        }
    }
}