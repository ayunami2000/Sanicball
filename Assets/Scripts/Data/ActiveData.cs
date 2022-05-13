using System.Runtime.Serialization.Formatters.Binary;
using SanicballCore;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sanicball.Data
{
    public class ActiveData : MonoBehaviour
    {
        #region Fields

        public List<RaceRecord> raceRecords = new List<RaceRecord>();

        //Pseudo-singleton pattern - this field accesses the current instance.
        private static ActiveData instance;

        //This data is saved to a json file
        private GameSettings gameSettings = new GameSettings();

        private KeybindCollection keybinds = new KeybindCollection();
        private MatchSettings matchSettings = MatchSettings.CreateDefault();

        //This data is set from the editor and remains constant
        [Header("Static data")]
        [SerializeField]
        private StageInfo[] stages;

        [SerializeField]
        private CharacterInfo[] characters;

        [SerializeField]
        private GameObject christmasHat;
        [SerializeField]
        private Material eSportsTrail;
        [SerializeField]
        private GameObject eSportsHat;
        [SerializeField]
        private AudioClip eSportsMusic;
        [SerializeField]
        private ESportMode eSportsPrefab;

        #endregion Fields

        #region Properties

        public static GameSettings GameSettings { get { return instance.gameSettings; } }
        public static KeybindCollection Keybinds { get { return instance.keybinds; } }
        public static MatchSettings MatchSettings { get { return instance.matchSettings; } set { instance.matchSettings = value; } }
        public static List<RaceRecord> RaceRecords { get { return instance.raceRecords; } }

        public static StageInfo[] Stages { get { return instance.stages; } }
        public static CharacterInfo[] Characters { get { return instance.characters; } }
        public static GameObject ChristmasHat { get { return instance.christmasHat; } }
        public static Material ESportsTrail { get { return instance.eSportsTrail; } }
        public static GameObject ESportsHat { get { return instance.eSportsHat; } }
        public static AudioClip ESportsMusic { get { return instance.eSportsMusic; } }
        public static ESportMode ESportsPrefab { get { return instance.eSportsPrefab; } }

        public static bool ESportsFullyReady
        {
            get
            {
                var possible = false;
                if (GameSettings.eSportsReady)
                {
                    var m = FindObjectOfType<Sanicball.Logic.MatchManager>();
                    if (m)
                    {
                        var players = m.Players;
                        foreach (var p in players)
                        {
                            if (p.CtrlType != SanicballCore.ControlType.None)
                            {
                                if (p.CharacterId == 13)
                                {
                                    possible = true;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
                return possible;
            }
        }

        #endregion Properties

        #region Unity functions

        //Make sure there is never more than one GameData object
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            LoadAll();
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        #endregion Unity functions

        #region Saving and loading

        public void LoadAll()
        {
            Load("GameSettings", ref gameSettings);
            Load("GameKeybinds", ref keybinds);
            Load("MatchSettings", ref matchSettings);
            Load("Records", ref raceRecords);
        }

        public void SaveAll()
        {
            Save("GameSettings", gameSettings);
            Save("GameKeybinds", keybinds);
            Save("MatchSettings", matchSettings);
            Save("Records", raceRecords);
        }

        private void Load<T>(string filename, ref T output)
        {
            string fullPath = Application.persistentDataPath + "/" + filename;
            if (File.Exists(fullPath))
            {
                //Deserialize from binary into a data object
                try
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    FileStream file = File.Open(fullPath, FileMode.Open);
                    var dataObj = (T)bf.Deserialize(file);
                    file.Close();
                    //Make sure an object was created, this would't end well with a null value
                    if (dataObj != null)
                    {
                        output = dataObj;
                        Debug.Log(filename + " loaded successfully.");
                    }
                    else
                    {
                        Debug.LogError("Failed to load " + filename + ": file is empty.");
                    }
                }
                catch (System.Runtime.Serialization.SerializationException ex)
                {
                    Debug.LogError("Failed to parse " + filename + "! Binary converter info: " + ex.Message);
                }
            }
            else
            {
                Debug.Log(filename + " has not been loaded - file not found.");
            }
        }

        private void Save(string filename, object objToSave)
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(Application.persistentDataPath + "/" + filename, FileMode.OpenOrCreate);
            bf.Serialize(file, objToSave);
            file.Close();
            Debug.Log(filename + " saved successfully.");
        }

        #endregion Saving and loading
    }
}
