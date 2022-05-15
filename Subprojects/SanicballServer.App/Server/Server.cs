using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//using System.Threading.Tasks;
using static SanicballCore.Cerealizer;
using SanicballCore.MatchMessages;
using SanicballServer;

namespace SanicballCore.Server
{
    public class LogArgs : EventArgs
    {
        public LogEntry Entry { get; }

        public LogArgs(LogEntry entry)
        {
            Entry = entry;
        }
    }
    
    public struct LogEntry
    {
        public DateTime Timestamp { get; }
        public string Message { get; }

        public LogEntry(DateTime timestamp, string message)
        {
            Timestamp = timestamp;
            Message = message;
        }
    }

    public class Server : IDisposable
    {
        public const string CONFIG_FILENAME = "ServerConfig.json";
        private const string SETTINGS_FILENAME = "MatchSettings.json";
        private const string MOTD_FILENAME = "MOTD.txt";
        private const int TICKRATE = 20;
        private const int STAGE_COUNT = 5; //Hardcoded stage count for now.. can't receive the actual count since it's part of a Unity prefab.
        private readonly CharacterTier[] characterTiers = new[] { //Hardcoded character tiers, same reason
            CharacterTier.Normal,       //Sanic
            CharacterTier.Normal,       //Knackles
            CharacterTier.Normal,       //Taels
            CharacterTier.Normal,       //Ame
            CharacterTier.Normal,       //Shedew
            CharacterTier.Normal,       //Roge
            CharacterTier.Normal,       //Asspio
            CharacterTier.Odd,          //Big
            CharacterTier.Odd,          //Aggmen
            CharacterTier.Odd,          //Chermy
            CharacterTier.Normal,       //Sulver
            CharacterTier.Normal,       //Bloze
            CharacterTier.Normal,       //Vactor
            CharacterTier.Hyperspeed,   //Super Sanic
            CharacterTier.Odd,       //Metal Sanic
            CharacterTier.Odd,          //Ogre
        };

        public event EventHandler<LogArgs> OnLog;

        //Server utilities
        private List<LogEntry> log = new List<LogEntry>();
        private Dictionary<string, CommandHandler> commandHandlers = new Dictionary<string, CommandHandler>();
        private Dictionary<string, string> commandHelp = new Dictionary<string, string>();
        private CommandQueue commandQueue;
        private Random random = new Random();

        //Server state
        private bool running;
        private bool debugMode;
        private List<ServClient> clients = new List<ServClient>();
        private List<ServPlayer> players = new List<ServPlayer>();
        private MatchSettings matchSettings;
        private string motd;
        private bool inRace;

        private ConcurrentDictionary<Guid, WebSocketWrapper> connectedClients
            = new ConcurrentDictionary<Guid, WebSocketWrapper>();

        public readonly ServerConfig Config;

        public int ConnectedClients => clients.Count;
        public bool InGame => inRace;

        #region Timers

        //Server browser ping timer
        private Stopwatch serverListPingTimer = new Stopwatch();
        private const float SERVER_BROWSER_PING_INTERVAL = 600;

        //Timer for starting a match by all players being ready
        private Stopwatch lobbyTimer = new Stopwatch();
        private const float LOBBY_MATCH_START_TIME = 3;

        //Timer for starting a match automatically
        private Stopwatch autoStartTimer = new Stopwatch();

        //Timeout for clients loading stage
        private Stopwatch stageLoadingTimeoutTimer = new Stopwatch();
        private const float STAGE_LOADING_TIMEOUT = 20;

        //Timer for going back to lobby at the end of a race
        private Stopwatch backToLobbyTimer = new Stopwatch();

        #endregion Timers

        public bool Running { get { return running; } }

        public Server(CommandQueue commandQueue, ServerConfig config)
        {
            this.commandQueue = commandQueue;
            this.Config = config;

            #region Command handlers

            AddCommandHandler("help",
            "help help help",
          cmd =>
            {
                if (cmd.Content.Trim() == string.Empty)
                {
                    Log("Available commands:");
                    foreach (var name in commandHandlers.Keys)
                    {
                        Log(name);
                    }
                    Log("Use 'help [command name]' for a command decription");
                }
                else
                {
                    string help;
                    if (commandHelp.TryGetValue(cmd.Content.Trim(), out help))
                    {
                        Log(help);
                    }
                }
            });
            AddCommandHandler("toggleDebug",
            "Debug mode displays a ton of extra technical output. Useful if you suspect something is wrong with the server.",
          cmd =>
            {
                debugMode = !debugMode;
                Log("Debug mode set to " + debugMode);
            });
            AddCommandHandler("stop",
            "Stops the server. I recommend stopping it this way - any other probably won't save the server log.",
          cmd =>
            {
                running = false;
            });
            AddCommandHandler("say",
            "Chat to clients on the server.",
            async cmd =>
            {
                if (cmd.Content.Trim() == string.Empty)
                {
                    Log("Usage: say [message]");
                }
                else
                {
                    await SendToAll(new ChatMessage("Server", ChatMessageType.System, cmd.Content));
                    Log("Chat message sent");
                }
            });
            AddCommandHandler("clients",
            "Displays a list of connected clients. (A client is a technical term another Sanicball instance)",
          cmd =>
            {
                Log(clients.Count + " connected client(s)");
                foreach (var client in clients)
                {
                    Log(client.Name);
                }
            });
            AddCommandHandler("players",
            "Displays a list of active players. Clients can have multiple players for splitscreen, or none at all to spectate.",
          cmd =>
                {
                    Log(clients.Count + " players(s) in match");
                });
            AddCommandHandler("kick",
            "Kicks a player from the server. Of course he could just re-join, but hopefully he'll get the message.",
          cmd =>
            {
                if (cmd.Content.Trim() == string.Empty)
                {
                    Log("Usage: kick [client name/part of name]");
                }
                else
                {
                    var matching = SearchClients(cmd.Content);
                    if (matching.Count == 0)
                    {
                        Log("No clients match your search.");
                    }
                    else if (matching.Count == 1)
                    {
                        Kick(matching[0], "Kicked by server");
                    }
                    else
                    {
                        Log("More than one client matches your search:");
                        foreach (var client in matching)
                        {
                            Log(client.Name);
                        }
                    }
                }
            });
            AddCommandHandler("returnToLobby",
            "Force exits any ongoing race.",
          async cmd =>
            {
                await ReturnToLobbyAsync();
            });
            AddCommandHandler("forceStart",
            "Force starts a race when in the lobby. Use this carefully, players may not be ready for racing",
          async cmd =>
            {
                if (inRace == false)
                {
                    Log("The race has been forcefully started.");
                    await LoadRaceAsync();
                }
                else
                {
                    Log("Race can only be force started in the lobby.");
                }
            });
            AddCommandHandler("showSettings",
            "Shows match settings. Settings like stage rotation are just shown as a number (Example: if StageRotationMode shows '1', it means 'Sequenced')",
          cmd =>
            {
                Log("not implemented");
            });

            AddCommandHandler("reloadMOTD",
            "Reloads message of the day from the default file, or optionally a custom file path.",
          cmd =>
            {
                var success = false;
                if (cmd.Content.Trim() != string.Empty)
                {
                    LoadMOTD(cmd.Content.Trim());
                }
                else
                {
                    LoadMOTD();
                }
            });
            AddCommandHandler("setStage",
            "Sets the stage by index. 0 = Green Hill, 1 = Flame Core, 2 = Ice Mountain, 3 = Rainbow Road, 4 = Dusty Desert",
          async cmd =>
            {
                int inputInt;
                if (int.TryParse(cmd.Content, out inputInt) && inputInt >= 0 && inputInt < STAGE_COUNT)
                {
                    matchSettings.StageId = inputInt;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Stage set to " + inputInt);
                }
                else
                {
                    Log("Usage: setStage [0-" + (STAGE_COUNT - 1) + "]");
                }
            });
            AddCommandHandler("setLaps",
            "Sets the number of laps per race. Laps are pretty long so 2 or 3 is recommended.",
          async cmd =>
            {
                int inputInt;
                if (int.TryParse(cmd.Content, out inputInt) && inputInt > 0)
                {
                    matchSettings.Laps = inputInt;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Lap count set to " + inputInt);
                }
                else
                {
                    Log("Usage: setLaps [>0]");
                }
            });
            AddCommandHandler("setAutoStartTime",
            "Sets the time required (in seconds) with enough players in the lobby before a race is automatically started.",
          async cmd =>
            {
                int inputInt;
                if (int.TryParse(cmd.Content, out inputInt) && inputInt > 0)
                {
                    matchSettings.AutoStartTime = inputInt;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Match auto start time set to " + inputInt);
                }
                else
                {
                    Log("Usage: setAutoStartTime [>0]");
                }
            });
            AddCommandHandler("setAutoStartMinPlayers",
            "Sets the minimum amount of players needed in the lobby before the auto start countdown begins.",
          async cmd =>
            {
                int inputInt;
                if (int.TryParse(cmd.Content, out inputInt) && inputInt > 0)
                {
                    matchSettings.AutoStartMinPlayers = inputInt;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Match auto start minimum players set to " + inputInt);
                }
                else
                {
                    Log("Usage: setAutoStartMinPlayers [>0]");
                }
            });
            AddCommandHandler("setStageRotationMode",
            "If not set to None, stages will change either randomly or in sequence every time the server returns to lobby.",
          async cmd =>
            {
                try
                {
                    var rotMode = (StageRotationMode)Enum.Parse(typeof(StageRotationMode), cmd.Content);
                    matchSettings.StageRotationMode = rotMode;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Stage rotation mode set to " + rotMode);
                }
                catch (Exception)
                {
                    var modes = Enum.GetNames(typeof(StageRotationMode));
                    var modesStr = string.Join("|", modes);
                    Log("Usage: setStageRotationMode [" + modesStr + "]");
                }
            });
            AddCommandHandler("setAllowedTiers",
            "Controls what ball tiers players can use.",
          async cmd =>
            {
                try
                {
                    var tiers = (AllowedTiers)Enum.Parse(typeof(AllowedTiers), cmd.Content);
                    matchSettings.AllowedTiers = tiers;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    await CorrectPlayerTiersAsync();
                    await Broadcast(GetAllowedTiersText());
                    Log("Allowed tiers set to " + tiers);
                }
                catch (Exception)
                {
                    var modes = Enum.GetNames(typeof(AllowedTiers));
                    var modesStr = string.Join("|", modes);
                    Log("Usage: setAllowedTiers [" + modesStr + "]");
                }
            });
            AddCommandHandler("setTierRotationMode",
            "If not None, allowed ball tiers will change (To either NormalOnly, OddOnly or HyperspeedOnly) each time the server returns to lobby. WeightedRandom has a 10/14 chance of picking NormalOnly, 3/14 of OddOnly and 1/14 of HyperspeedOnly.",
          async cmd =>
            {
                try
                {
                    var mode = (TierRotationMode)Enum.Parse(typeof(TierRotationMode), cmd.Content);
                    matchSettings.TierRotationMode = mode;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Tier rotation mode set to " + mode);
                }
                catch (Exception)
                {
                    var modes = Enum.GetNames(typeof(TierRotationMode));
                    var modesStr = string.Join("|", modes);
                    Log("Usage: setTierRotationMode [" + modesStr + "]");
                }
            });
            AddCommandHandler("setVoteRatio",
            "Sets the fraction of players required to select 'return to lobby' before the server returns to lobby. 1.0, the default, requires all players. Something like 0.8 would be good for a very big server.",
          async cmd =>
            {
                float newVoteRatio;
                if (float.TryParse(cmd.Content, out newVoteRatio) && newVoteRatio >= 0f && newVoteRatio <= 1f)
                {
                    matchSettings.VoteRatio = newVoteRatio;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Match vote ratio set to " + newVoteRatio);
                }
                else
                {
                    Log("Usage: setVoteRatio [0.0-1.0]");
                }
            });
            AddCommandHandler("setDisqualificationTime",
            "Sets the time a player needs to loiter around without passing any checkpoints before they are disqualified from a race. If too low, players might get DQ'd just for being slow. 0 disables disqualifying.",
          async cmd =>
            {
                int inputInt;
                if (int.TryParse(cmd.Content, out inputInt) && inputInt >= 0)
                {
                    matchSettings.DisqualificationTime = inputInt;
                    SaveMatchSettings();
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                    Log("Disqualification time set to " + inputInt);
                }
                else
                {
                    Log("Usage: setDisqualificationTime [>=0]");
                }
            });

            #endregion Command handlers

        }

        public async Task Start()
        {
            LoadMatchSettings();

            LoadMOTD();

            //Welcome message
            Log("Welcome! Type 'help' for a list of commands. Type 'stop' to shut down the server.");

            running = true;

#if DEBUG
            debugMode = true;
#endif
            await MessageLoopAsync();
        }

        // TODO: Random MOTD
        private void LoadMOTD(string path = MOTD_FILENAME)
        {
            if (File.Exists(path))
            {
                using (var sr = new StreamReader(path))
                {
                    motd = sr.ReadToEnd();
                    Log("Loaded message of the day from " + path);
                }
            }
            else
            {
                if (path == MOTD_FILENAME)
                {
                    Log("No message of the day found. You can display a message to joining players by creating a file named '" + MOTD_FILENAME + "' next to the server executable.");
                }
                else
                {
                    Log("Could not load MOTD from this file.");
                }
            }
        }


        public Task ConnectClientAsync(WebSocket socket)
        {
            var wrapper = new WebSocketWrapper(socket);
            wrapper.Start();

            Log($"Recieved a socket connection from {socket}, requesting validation.");

            connectedClients[wrapper.Id] = wrapper;

            return Task.CompletedTask;
        }

        private async Task MessageLoopAsync()
        {
            while (running)
            {
                Thread.Sleep(1000 / TICKRATE);

                //Check server browser ping timer
                if (serverListPingTimer.IsRunning)
                {
                    if (serverListPingTimer.Elapsed.TotalSeconds >= SERVER_BROWSER_PING_INTERVAL)
                    {
                        serverListPingTimer.Reset();
                        serverListPingTimer.Start();
                    }
                }

                //Check lobby timer
                if (lobbyTimer.IsRunning)
                {
                    if (lobbyTimer.Elapsed.TotalSeconds >= LOBBY_MATCH_START_TIME)
                    {
                        //Log("The race has been started by all players being ready.", LogLevel.Debug);
                        await LoadRaceAsync();
                    }
                }

                //Check stage loading timer
                if (stageLoadingTimeoutTimer.IsRunning)
                {
                    if (stageLoadingTimeoutTimer.Elapsed.TotalSeconds >= STAGE_LOADING_TIMEOUT)
                    {
                        await SendToAll(new StartRaceMessage());
                        stageLoadingTimeoutTimer.Reset();

                        foreach (var c in clients.Where(a => a.CurrentlyLoadingStage))
                        {
                            Kick(c, "Took too long to load the race");
                        }
                    }
                }


                //Check auto start timer
                if (autoStartTimer.IsRunning)
                {
                    if (autoStartTimer.Elapsed.TotalSeconds >= matchSettings.AutoStartTime)
                    {
                        //Log("The race has been automatically started.", LogLevel.Debug);
                        await LoadRaceAsync();
                    }
                }

                //Check back to lobby timer
                if (backToLobbyTimer.IsRunning)
                {
                    if (backToLobbyTimer.Elapsed.TotalSeconds >= matchSettings.AutoReturnTime)
                    {
                        await ReturnToLobbyAsync();
                        backToLobbyTimer.Reset();
                    }
                }

                //Check racing timeout timers
                foreach (var p in players)
                {
                    if (matchSettings.DisqualificationTime > 0)
                    {
                        if (!p.TimeoutMessageSent && p.RacingTimeout.Elapsed.TotalSeconds > matchSettings.DisqualificationTime / 2.0f)
                        {
                            await SendToAll(new RaceTimeoutMessage(p.ClientGuid, p.CtrlType, matchSettings.DisqualificationTime / 2.0f));
                            p.TimeoutMessageSent = true;
                        }
                        if (p.RacingTimeout.Elapsed.TotalSeconds > matchSettings.DisqualificationTime)
                        {
                            Log("A player was too slow to race and has been disqualified.");

                            await FinishRaceAsync(p);
                            await SendToAll(new DoneRacingMessage(p.ClientGuid, p.CtrlType, 0, true));
                        }
                    }
                }

                //Check command queue
                Command cmd;
                while ((cmd = commandQueue.ReadNext()) != null)
                {
                    CommandHandler handler;
                    if (commandHandlers.TryGetValue(cmd.Name, out handler))
                    {
                        handler(cmd);
                    }
                    else
                    {
                        Log("Command '" + cmd.Name + "' not found.");
                    }
                }

                //Check network message queue
                foreach (var client in connectedClients)
                {
                    while (client.Value.Dequeue(out var msg))
                    {
                        try
                        {
                            using (var dest = new MemoryStream())
                            using (var writer = new BinaryWriter(dest, Encoding.UTF8))
                            {
                                switch (msg.Type)
                                {
                                    case MessageTypes.Connect:
                                        ClientInfo clientInfo = UnCereal<ClientInfo>(ReadAllBytes(msg.Reader));
                                        if (clientInfo == null)
                                        {
                                            Log("Error reading client connection approval. Client rejected.");

                                            writer.Write(false);
                                            writer.Write("Invalid client info! You are likely using a different game version than the server.");
                                            await client.Value.Send(MessageTypes.Validate, writer, dest);

                                            Log($"Refused to validate {client.Key}");
                                            break;
                                        }

                                        if (clientInfo.Version != GameVersion.AS_FLOAT || clientInfo.IsTesting != GameVersion.IS_TESTING)
                                        {
                                            writer.Write(false);
                                            writer.Write("Invalid game version.");
                                            await client.Value.Send(MessageTypes.Validate, writer, dest);

                                            Log($"Refused to validate {client.Key}");
                                            break;
                                        }

                                        float autoStartTimeLeft = 0;
                                        if (autoStartTimer.IsRunning)
                                        {
                                            autoStartTimeLeft = matchSettings.AutoStartTime - (float)autoStartTimer.Elapsed.TotalSeconds;
                                        }
                                        var clientStates = new List<MatchClientState>();
                                        foreach (var c in clients)
                                        {
                                            clientStates.Add(new MatchClientState(c.Guid, c.Name));
                                        }
                                        var playerStates = new List<MatchPlayerState>();
                                        foreach (var p in players)
                                        {
                                            playerStates.Add(new MatchPlayerState(p.ClientGuid, p.CtrlType, p.ReadyToRace, p.CharacterId));
                                        }


                                        var state = new MatchState(clientStates, playerStates, matchSettings, inRace, autoStartTimeLeft);
                                        var str = Cereal(state);
                                        writer.Write(str);

                                        await client.Value.Send(MessageTypes.Connect, writer, dest);

                                        //Log("Sent match state to newly connected client", LogLevel.Debug);

                                        break;
                                    case MessageTypes.Disconnect:
                                        var statusMsg = msg?.Reader?.ReadString() ?? "Client disconnected";
                                        var associatedClient = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
                                        if (associatedClient != null)
                                        {
                                            Log($"Disconnected {associatedClient.Name}");

                                            //Remove all players created by this client
                                            players.RemoveAll(a => a.ClientGuid == associatedClient.Guid);

                                            //If no players are left and we're in a race, return to lobby
                                            if (players.Count == 0 && inRace)
                                            {
                                                //Log("No players left in race!", LogLevel.Debug);
                                                await ReturnToLobbyAsync();
                                            }

                                            //If there are now less players than AutoStartMinPlayers, stop the auto start timer
                                            if (players.Count < matchSettings.AutoStartMinPlayers && autoStartTimer.IsRunning)
                                            {
                                                //Log("Too few players, match auto start timer stopped", LogLevel.Debug);
                                                await StopAutoStartTimerAsync();
                                            }

                                            //Remove the client
                                            clients.Remove(associatedClient);

                                            //Tell connected clients to remove the client+players
                                            await SendToAll(new ClientLeftMessage(associatedClient.Guid));
                                            await Broadcast(associatedClient.Name + " has left the match (" + statusMsg + ")");
                                        }
                                        else
                                        {
                                            //Log("Unknown client disconnected (Client was most likely not done connecting)", LogLevel.Debug);
                                        }

                                        connectedClients.Remove(client.Key, out _);

                                        break;
                                    case MessageTypes.PlayerMovement:
                                        writer.Write(msg.GetBytes());
                                        var recipients = clients.Where(a => a.Connection.Id != msg.Source).ToList();
                                        if (recipients.Count > 0)
                                        {
                                            foreach (var item in recipients)
                                            {
                                                await item.Connection.SendAsync(msg);
                                            }
                                        }

                                        break;
                                    case MessageTypes.Match:
                                        double timestamp = msg.Reader.ReadInt64();
                                        MatchMessage matchMessage = UnCereal<MatchMessage>(ReadAllBytes(msg.Reader));
                                        if (matchMessage == null)
                                        {
                                            Log("Failed to deserialize received match message.");
                                            continue; //Skip to next message in queue
                                        }

                                        if (matchMessage is ClientJoinedMessage)
                                        {
                                            await ClientJoinedAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is PlayerJoinedMessage)
                                        {
                                            await PlayerJoinedAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is PlayerLeftMessage)
                                        {
                                            await PlayerLeftAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is CharacterChangedMessage)
                                        {
                                            await CharacterChangedAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is ChangedReadyMessage)
                                        {
                                            await ChangedReadyAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is SettingsChangedMessage)
                                        {
                                            //var castedMsg = (SettingsChangedMessage)matchMessage;
                                            //matchSettings = castedMsg.NewMatchSettings;
                                            //SendToAll(matchMessage);

                                            //Log("A player tried to change match settings", LogLevel.Debug);
                                        }

                                        if (matchMessage is StartRaceMessage)
                                        {
                                            await StartRaceAsync(msg);
                                        }

                                        if (matchMessage is ChatMessage)
                                        {
                                            await ChatMessageAsync(msg, matchMessage);
                                        }

                                        if (matchMessage is LoadLobbyMessage)
                                        {
                                            await LoadLobbyAsync(msg);
                                        }

                                        if (matchMessage is CheckpointPassedMessage)
                                        {
                                            await CheckpointPassedAsync(matchMessage);
                                        }

                                        if (matchMessage is DoneRacingMessage)
                                        {
                                            await DoneRacingAsync(matchMessage);
                                        }

                                        break;

                                    default:
                                        //Log("Received data message of unknown type");
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("An error occured processing a message from " + client.Key + ": " + ex);
                            //_logger.LogError(ex, $"An error occured processing a message from {client.Key}");
                        }
                    }
                }
            }
            Environment.Exit(0);
        }

        private async Task ClientJoinedAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (ClientJoinedMessage)matchMessage;

            var newClient = new ServClient(castedMsg.ClientGuid, castedMsg.ClientName, connectedClients[msg.Source]);
            clients.Add(newClient);

            await Broadcast(castedMsg.ClientName + " has joined the match");

            if (motd != null)
            {
                await Whisper(newClient, "Server's message of the day:");
                await Whisper(newClient, motd);
            }
            else
            {
                await Whisper(newClient, "Welcome to the server!");
            }
            await Whisper(newClient, GetAllowedTiersText());
            await SendToAll(matchMessage);
        }

        private async Task PlayerJoinedAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (PlayerJoinedMessage)matchMessage;

            //Check if the message was sent from the same client it wants to act for
            var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);

            if (client == null || castedMsg.ClientGuid != client.Guid)
            {
                Log("Received PlayerJoinedMessage with invalid ClientGuid property");
            }
            else
            {
                if (VerifyCharacterTier(castedMsg.InitialCharacter))
                {
                    players.Add(new ServPlayer(castedMsg.ClientGuid, castedMsg.CtrlType, castedMsg.InitialCharacter));
                    //Log("Player " + client.Name + " (" + castedMsg.CtrlType + ") joined", LogLevel.Debug);
                    await SendToAll(matchMessage);

                    if (players.Count >= matchSettings.AutoStartMinPlayers && !autoStartTimer.IsRunning && matchSettings.AutoStartTime > 0)
                    {
                        //Log("Match will auto start in " + matchSettings.AutoStartTime + " seconds.", LogLevel.Debug);
                        await StartAutoStartTimerAsync();
                    }
                }
                else
                {
                    await Whisper(client, "You cannot join with this character - " + GetAllowedTiersText());
                }

            }
        }

        private async Task PlayerLeftAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (PlayerLeftMessage)matchMessage;

            //Check if the message was sent from the same client it wants to act for
            var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
            if (client == null || castedMsg.ClientGuid != client.Guid)
            {
                Log("Received PlayerLeftMessage with invalid ClientGuid property");
            }
            else
            {
                var player = players.FirstOrDefault(a => a.ClientGuid == castedMsg.ClientGuid && a.CtrlType == castedMsg.CtrlType);
                players.Remove(player);
                //Log("Player " + client.Name + " (" + castedMsg.CtrlType + ") left", LogLevel.Debug);
                await SendToAll(matchMessage);

                if (players.Count < matchSettings.AutoStartMinPlayers && autoStartTimer.IsRunning)
                {
                    //Log("Too few players, match auto start timer stopped", LogLevel.Debug);
                    await StopAutoStartTimerAsync();
                }
            }
        }

        private async Task CharacterChangedAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (CharacterChangedMessage)matchMessage;

            //Check if the message was sent from the same client it wants to act for
            var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
            if (client == null || client.Guid != castedMsg.ClientGuid)
            {
                Log("Received CharacterChangedMessage with invalid ClientGuid property");
            }
            else
            {
                var player = players.FirstOrDefault(a => a.ClientGuid == castedMsg.ClientGuid && a.CtrlType == castedMsg.CtrlType);
                if (player != null)
                {
                    if (VerifyCharacterTier(castedMsg.NewCharacter))
                    {
                        player.CharacterId = castedMsg.NewCharacter;
                        //Log("Player " + client.Name + " (" + castedMsg.CtrlType + ") set character to " + castedMsg.NewCharacter, LogLevel.Debug);
                        await SendToAll(matchMessage);
                    }
                    else
                    {
                        await Whisper(client, "You can't use this character - " + GetAllowedTiersText());
                        //Log("Player " + client.Name + " (" + castedMsg.CtrlType + ") tried to set character to " + castedMsg.NewCharacter + " but the character's tier is not allowed", LogLevel.Debug);
                    }
                }
            }
        }

        private async Task ChangedReadyAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (ChangedReadyMessage)matchMessage;

            //Check if the message was sent from the same client it wants to act for
            var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
            if (client == null || client.Guid != castedMsg.ClientGuid)
            {
                Log("Received ChangeReadyMessage with invalid ClientGuid property");
            }
            else
            {
                var player = players.FirstOrDefault(a => a.ClientGuid == castedMsg.ClientGuid && a.CtrlType == castedMsg.CtrlType);
                if (player != null)
                {
                    var index = players.IndexOf(player);
                    players[index].ReadyToRace = castedMsg.Ready;
                }
                //Log("Player " + client.Name + " (" + castedMsg.CtrlType + ") set ready to " + castedMsg.Ready, LogLevel.Debug);

                //Start lobby timer if all players are ready - otherwise reset it if it's running
                var allPlayersReady = players.All(a => a.ReadyToRace);
                if (allPlayersReady)
                {
                    lobbyTimer.Start();
                    //Log("All players ready, timer started", LogLevel.Debug);
                }
                else
                {
                    if (lobbyTimer.IsRunning)
                    {
                        lobbyTimer.Reset();
                        //Log("Not all players are ready, timer stopped", LogLevel.Debug);
                    }
                }

                await SendToAll(matchMessage);
            }
        }

        private async Task StartRaceAsync(MessageWrapper msg)
        {
            var clientsLoadingStage = clients.Count(a => a.CurrentlyLoadingStage);
            if (clientsLoadingStage > 0)
            {
                var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
                client.CurrentlyLoadingStage = false;
                clientsLoadingStage--;
                if (clientsLoadingStage > 0)
                {
                    //Log("Waiting for " + clientsLoadingStage + " client(s) to load", LogLevel.Debug);
                }
                else
                {
                    Log("Starting race!");
                    await SendToAll(new StartRaceMessage());
                    stageLoadingTimeoutTimer.Reset();
                    //Indicate that all currently active players are racing
                    players.ForEach(a =>
                    {
                        a.CurrentlyRacing = true;
                        a.RacingTimeout.Start();
                    });
                }
            }
        }

        private async Task ChatMessageAsync(MessageWrapper msg, MatchMessage matchMessage)
        {
            var castedMsg = (ChatMessage)matchMessage;
            Log(string.Format("[{0}] {1}: {2}", castedMsg.Type, castedMsg.From, castedMsg.Text));

            if (castedMsg.Text.ToLower().Contains("shrek") && VerifyCharacterTier(15))
            {
                var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
                var playersFromClient = players.Where(a => a.ClientGuid == client.Guid).ToArray();
                foreach (var p in playersFromClient)
                {
                    p.CharacterId = 15;
                    await SendToAll(new CharacterChangedMessage(p.ClientGuid, p.CtrlType, 15));
                }
            }

            await SendToAll(matchMessage);
        }

        private async Task LoadLobbyAsync(MessageWrapper msg)
        {
            var client = clients.FirstOrDefault(a => a.Connection.Id == msg.Source);
            if (!client.WantsToReturnToLobby)
            {
                client.WantsToReturnToLobby = true;

                var clientsRequiredToReturn = (int)(clients.Count * matchSettings.VoteRatio);

                if (clients.Count(a => a.WantsToReturnToLobby) >= clientsRequiredToReturn)
                {
                    await Broadcast("Returning to lobby by user vote.");
                    await ReturnToLobbyAsync();
                }
                else
                {
                    var clientsNeeded = clientsRequiredToReturn - clients.Count(a => a.WantsToReturnToLobby);
                    await Broadcast(client.Name + " wants to return to the lobby. " + clientsNeeded + " more vote(s) needed.");
                }
            }
        }

        private async Task CheckpointPassedAsync(MatchMessage matchMessage)
        {
            var castedMsg = (CheckpointPassedMessage)matchMessage;

            var player = players.FirstOrDefault(a => a.ClientGuid == castedMsg.ClientGuid && a.CtrlType == castedMsg.CtrlType);
            if (player != null)
            {
                //As long as all players are racing, timeouts should be reset.
                if (players.All(a => a.CurrentlyRacing))
                {
                    player.RacingTimeout.Reset();
                    player.RacingTimeout.Start();
                    if (player.TimeoutMessageSent)
                    {
                        player.TimeoutMessageSent = false;
                        await SendToAll(new RaceTimeoutMessage(player.ClientGuid, player.CtrlType, 0));
                    }
                }
                await SendToAll(matchMessage);
            }
            else
            {
                //Log("Received CheckpointPassedMessage for invalid player", LogLevel.Debug);
            }
        }

        private async Task DoneRacingAsync(MatchMessage matchMessage)
        {
            var castedMsg = (DoneRacingMessage)matchMessage;
            var player = players.FirstOrDefault(a => a.ClientGuid == castedMsg.ClientGuid && a.CtrlType == castedMsg.CtrlType);
            if (player != null)
            {
                await FinishRaceAsync(player);
            }
            await SendToAll(matchMessage);
        }

        #region Gameplay methods

        private async Task LoadRaceAsync()
        {
            lobbyTimer.Reset();
            await StopAutoStartTimerAsync();
            await SendToAll(new LoadRaceMessage());
            inRace = true;
            //Set ready to false for all players
            players.ForEach(a => a.ReadyToRace = false);
            //Wait for clients to load the stage
            clients.ForEach(a => a.CurrentlyLoadingStage = true);
            //Start timeout timer
            stageLoadingTimeoutTimer.Start();
        }

        private async Task ReturnToLobbyAsync()
        {
            if (inRace)
            {
                Log("Returned to lobby");
                inRace = false;
                await SendToAll(new LoadLobbyMessage());

                backToLobbyTimer.Reset();

                players.ForEach(a =>
                {
                    a.CurrentlyRacing = false;
                    a.RacingTimeout.Reset();
                    a.TimeoutMessageSent = false;
                });
                clients.ForEach(a => a.WantsToReturnToLobby = false);

                var matchSettingsChanged = false;

                //Stage rotation
                switch (matchSettings.StageRotationMode)
                {
                    case StageRotationMode.Random:
                        //Log("Picking random stage", LogLevel.Debug);
                        var newStage = matchSettings.StageId;

                        while (newStage == matchSettings.StageId)
                            newStage = random.Next(STAGE_COUNT);

                        matchSettings.StageId = newStage;
                        matchSettingsChanged = true;
                        break;

                    case StageRotationMode.Sequenced:
                        //Log("Picking next stage", LogLevel.Debug);
                        var nextStage = matchSettings.StageId + 1;
                        if (nextStage >= STAGE_COUNT) nextStage = 0;
                        matchSettings.StageId = nextStage;
                        matchSettingsChanged = true;
                        break;
                }

                //Tier rotation
                var newTiers = matchSettings.AllowedTiers;
                switch (matchSettings.TierRotationMode)
                {
                    case TierRotationMode.Cycle:
                        switch (matchSettings.AllowedTiers)
                        {
                            case AllowedTiers.NormalOnly:
                                newTiers = AllowedTiers.OddOnly;
                                break;
                            case AllowedTiers.OddOnly:
                                newTiers = AllowedTiers.HyperspeedOnly;
                                break;
                            default:
                                newTiers = AllowedTiers.NormalOnly;
                                break;
                        }
                        break;
                    case TierRotationMode.Random:
                        var rand = random.Next() % 3;
                        switch (rand)
                        {
                            case 0:
                                newTiers = AllowedTiers.NormalOnly;
                                break;
                            case 1:
                                newTiers = AllowedTiers.OddOnly;
                                break;
                            case 2:
                                newTiers = AllowedTiers.HyperspeedOnly;
                                break;
                        }
                        break;
                    case TierRotationMode.WeightedRandom:
                        var choices = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2 };
                        var choice = choices[random.Next() % choices.Length];
                        switch (choice)
                        {
                            case 0:
                                newTiers = AllowedTiers.NormalOnly;
                                break;
                            case 1:
                                newTiers = AllowedTiers.OddOnly;
                                break;
                            case 2:
                                newTiers = AllowedTiers.HyperspeedOnly;
                                break;
                        }
                        break;
                }
                if (newTiers != matchSettings.AllowedTiers)
                {
                    matchSettings.AllowedTiers = newTiers;
                    matchSettingsChanged = true;
                    await CorrectPlayerTiersAsync();
                    await Broadcast(GetAllowedTiersText());
                }

                if (matchSettingsChanged)
                {
                    await SendToAll(new SettingsChangedMessage(matchSettings));
                }

                if (players.Count >= matchSettings.AutoStartMinPlayers && matchSettings.AutoStartTime > 0)
                {
                    //Log("There are still players, autoStartTimer started", LogLevel.Debug);
                    await StartAutoStartTimerAsync();
                }
            }
            else
            {
                Log("Already in lobby");
            }
        }

        private async Task StartAutoStartTimerAsync()
        {
            autoStartTimer.Reset();
            autoStartTimer.Start();
            await SendToAll(new AutoStartTimerMessage(true));
        }

        private async Task StopAutoStartTimerAsync()
        {
            autoStartTimer.Reset();
            await SendToAll(new AutoStartTimerMessage(false));
        }

        private async Task FinishRaceAsync(ServPlayer p)
        {
            p.CurrentlyRacing = false;
            p.RacingTimeout.Reset();
            await SendToAll(new RaceTimeoutMessage(p.ClientGuid, p.CtrlType, 0));

            var playersStillRacing = players.Count(a => a.CurrentlyRacing);
            if (playersStillRacing == 0)
            {
                Log("All players are done racing.");
                if (matchSettings.AutoReturnTime > 0)
                {
                    await Broadcast("Returning to lobby in " + matchSettings.AutoReturnTime + " seconds");
                    backToLobbyTimer.Start();
                }
            }
            else
            {
                Log(playersStillRacing + " players(s) still racing");
            }
        }



        #endregion Gameplay methods

        #region Utility methods

        private async Task SendToAll(MatchMessage matchMsg)
        {
            //Log("Sending message of type " + matchMsg.GetType() + " to " + clients.Count + " connection(s)", LogLevel.Debug);
            var matchMsgSerialized = Cereal(matchMsg);

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, false))
            {
                writer.Write(DateTime.UtcNow.Ticks);
                writer.Write(matchMsgSerialized);
                writer.Flush();

                var data = memory.ToArray();
                foreach (var item in clients)
                {
                    await item.Connection.SendAsync(MessageTypes.Match, data);
                }
            }
        }

        private async Task SendTo(MatchMessage matchMsg, ServClient reciever)
        {
            //Log("Sending message of type " + matchMsg.GetType() + " to client " + reciever.Name, LogLevel.Debug);
            var matchMsgSerialized = Cereal(matchMsg);

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, false))
            {
                writer.Write(DateTime.UtcNow.Ticks);
                writer.Write(matchMsgSerialized);
                writer.Flush();

                var data = memory.ToArray();
                await reciever.Connection.SendAsync(MessageTypes.Match, data);
            }
        }

        /// <summary>
        /// Logs a string and sends it as a chat message to all clients.
        /// </summary>
        /// <param name="text"></param>
        private Task Broadcast(string text)
        {
            Log(text);
            return SendToAll(new ChatMessage("Server", ChatMessageType.System, text));
        }

        private Task Whisper(ServClient reciever, string text)
        {
            //Log("Sending whisper to client " + reciever.Name + "(Text: " + text + ")", LogLevel.Debug);
            return SendTo(new ChatMessage("Server", ChatMessageType.System, text), reciever);
        }

        /// <summary>
        /// Writes a message to the server log.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public void Log(object message)
        {
            Console.WriteLine(message);
        }

        private List<ServClient> SearchClients(string name)
        {
            return clients.Where(a => a.Name.Contains(name)).ToList();
        }

        public void AddCommandHandler(string commandName, string help, CommandHandler handler)
        {
            commandHandlers.Add(commandName, handler);
            commandHelp.Add(commandName, help);
        }

        public void Kick(ServClient client, string reason)
        {
            client.Connection.DisconnectAsync(reason).GetAwaiter().GetResult();
        }

        private async Task CorrectPlayerTiersAsync()
        {
            foreach (var player in players)
            {
                if (!VerifyCharacterTier(player.CharacterId))
                {
                    var client = clients.FirstOrDefault(a => a.Guid == player.ClientGuid);
                    if (client != null)
                    {
                        for (var i = 0; i < characterTiers.Length; i++)
                        {
                            if (VerifyCharacterTier(i))
                            {
                                player.CharacterId = i;
                                await SendToAll(new CharacterChangedMessage(player.ClientGuid, player.CtrlType, i));
                                await Whisper(client, "Your character is not allowed and has been automatically changed.");
                                break;
                            }
                        }
                    }
                }
            }
        }

        private bool VerifyCharacterTier(int id)
        {
            var t = characterTiers[id];
            switch (matchSettings.AllowedTiers)
            {
                case AllowedTiers.All:
                    return true;
                case AllowedTiers.NormalOnly:
                    return t == CharacterTier.Normal;
                case AllowedTiers.OddOnly:
                    return t == CharacterTier.Odd;
                case AllowedTiers.HyperspeedOnly:
                    return t == CharacterTier.Hyperspeed;
                case AllowedTiers.NoHyperspeed:
                    return t != CharacterTier.Hyperspeed;
                default:
                    return true;
            }
        }

        private string GetAllowedTiersText()
        {
            switch (matchSettings.AllowedTiers)
            {
                case AllowedTiers.NormalOnly:
                    return "Only characters from the Normal tier are allowed.";
                case AllowedTiers.OddOnly:
                    return "Only characters from the Odd tier are allowed.";
                case AllowedTiers.HyperspeedOnly:
                    return "Only characters from the Hyperspeed tier are allowed.";
                case AllowedTiers.NoHyperspeed:
                    return "Any character NOT from the Hyperspeed tier is allowed.";
                default:
                    return "All characters are allowed.";
            }
        }
        #endregion Utility methods

        private void SaveMatchSettings()
        {
            File.WriteAllBytes(SETTINGS_FILENAME, Cereal(matchSettings));
        }

        private void LoadMatchSettings()
        {
            if (File.Exists(SETTINGS_FILENAME))
            {
                matchSettings = UnCereal<MatchSettings>(File.ReadAllBytes(SETTINGS_FILENAME));
            }
            else
            {
                matchSettings = MatchSettings.CreateDefault();
            }
        }

        public void Dispose()
        {
            Log("Saving match settings...");
            SaveMatchSettings();

            foreach (var item in connectedClients)
            {
                item.Value.DisconnectAsync("Server is shutting down").GetAwaiter().GetResult();
            }

            Log("The server has been closed.");

            //Write server log
            Directory.CreateDirectory("Logs" + System.IO.Path.DirectorySeparatorChar);
            using (var writer = new StreamWriter("Logs" + System.IO.Path.DirectorySeparatorChar + DateTime.Now.ToString("MM-dd-yyyy_HH-mm-ss") + ".txt"))
            {
                foreach (var entry in log)
                {
                    writer.WriteLine(entry.Timestamp + " - " + entry.Message);
                }
            }
        }
    }
}
