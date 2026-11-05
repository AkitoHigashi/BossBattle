using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima.Editor
{
    /// <summary>Player 2との実接続で、新しい部屋単位の接続寿命を確認する。</summary>
    [InitializeOnLoad]
    public static class RoomConnectionLifecycleCheck
    {
        private static readonly string Project = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        private static readonly bool Guest = Project.Contains("/Library/VP/");
        private static readonly string Root = Guest ? Project.Substring(0, Project.IndexOf("/Library/VP/", StringComparison.Ordinal)) : Project;
        private static string PathFor(string name) => Root + "/Temp/RoomLifecycleCheck." + name;
        private static ushort TestPort => ushort.Parse(File.ReadAllText(PathFor("port")));
        private static NetworkManager _network;
        private static RoomClient _rooms;
        private static RoomConnectionLifecycle _session;
        private static int _step, _round;
        private static double _deadline, _next, _wait;
        private static string _lastCommand;
        private static ILobbyCharacterSelection _selection;
        private static IReadOnlyDictionary<int, string> _selected = new Dictionary<int, string>();
        private static bool _hostPicked;
        private static bool _abruptChecked;

        static RoomConnectionLifecycleCheck()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.delayCall += () =>
            {
                if (Guest || !File.Exists(PathFor("launch"))) return;
                File.Delete(PathFor("launch"));
                Run();
            };
        }

        [MenuItem("Tools/Yajima/Check Room Connection Lifecycle (Player 2)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "command", "guest", "result" }) File.WriteAllText(PathFor(name), "");
            using (var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
                File.WriteAllText(PathFor("port"), ((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port.ToString());
            File.WriteAllText(PathFor("running"), "run");
            EditorSceneManager.OpenScene(RoomSceneTransition.BrowserPath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(PathFor("running"))) return;
            _step = _round = 0; _next = _wait = 0; _lastCommand = null;
            _network = null; _rooms = null; _session = null;
            _selection = null; _selected = new Dictionary<int, string>(); _hostPicked = false;
            _abruptChecked = false;
            _deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup > _deadline) throw new Exception($"Timeout {(Guest ? "guest" : "host")} step {_step} / round {_round}");
                if (EditorApplication.timeSinceStartup < _next) return;
                _next = EditorApplication.timeSinceStartup + 0.15;
                if (_session == null)
                {
                    _network = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
                    if (_network == null) return;
                    _rooms = _network.GetComponent<RoomClient>();
                    _session = _network.GetComponent<RoomConnectionLifecycle>();
                    if (_session == null) throw new Exception("Missing lifecycle on prefab");
                    _selection = _network.GetComponents<MonoBehaviour>().OfType<ILobbyCharacterSelection>().Single();
                    _selection.Changed += selections => _selected = selections;
                    var manager = _network.GetComponents<MonoBehaviour>().Single(component =>
                        component.GetType().FullName == "NCG.Character.Selection.CharacterSelectionManager");
                    var serialized = new SerializedObject(manager);
                    // ホストの初期値はC、参加端末側はD。実辞書にはサーバーのCが入るはず。
                    serialized.FindProperty("_defaultCharacterType").enumValueIndex = Guest ? 3 : 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (Guest) GuestTick(); else HostTick();
            }
            catch (Exception error)
            {
                if (Guest) File.WriteAllText(PathFor("guest"), "FAIL: " + error);
                else Finish("FAIL: " + error);
                EditorApplication.update -= Tick;
            }
        }

        private static bool Browser => SceneManager.GetActiveScene().path == RoomSceneTransition.BrowserPath;
        private static bool Game => SceneManager.GetActiveScene().path == IngameRoomExitCoordinator.IngamePath;
        private static bool Lobby => SceneManager.GetActiveScene().path == RoomSceneTransition.LobbyPath;
        private static bool FullyStopped => _network.TransportManager.Transport.GetConnectionState(false) == LocalConnectionState.Stopped &&
            _network.TransportManager.Transport.GetConnectionState(true) == LocalConnectionState.Stopped;
        private static bool InRoom => _session.Phase == RoomConnectionLifecycle.ConnectionPhase.InRoom && _rooms.HasRoom;

        private static void HostTick()
        {
            string guest = File.ReadAllText(PathFor("guest"));
            if (guest.StartsWith("FAIL")) throw new Exception(guest);
            switch (_step)
            {
                case 0:
                    if (!FullyStopped || !_session.IsIdle) throw new Exception("Browser must start disconnected");
                    _session.CreateAndHost("", 4, TestPort);
                    if (!FullyStopped || !_session.IsIdle) throw new Exception("Invalid create started server");
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot("Temp/RoomLifecycle.Browser.png");
                    _step++; break;
                case 1:
                    _hostPicked = false;
                    SetBrowserInput("RoomName", "Room lifecycle " + _round);
                    SetBrowserInput("Port", TestPort.ToString());
                    ClickBrowser("Create"); _step++; break;
                case 2:
                    if (!InRoom || !Lobby) return;
                    if (!_session.IsHost || !_network.ServerManager.Started || !_network.ClientManager.Connection.IsAuthenticated)
                        throw new Exception("Create did not start host and room together");
                    int self = _network.ClientManager.Connection.ClientId;
                    if (!_hostPicked)
                    {
                        if (!_selected.TryGetValue(self, out string initial) || initial != "CharacterC" ||
                            _rooms.CurrentRoom.Members[0].Character != 2) return;
                        _selection.Select(1); // 他の人の入室で、この手動選択を初期値へ戻してはいけない。
                        _hostPicked = true;
                        return;
                    }
                    if (!_selected.TryGetValue(self, out string chosen) || chosen != "CharacterB" ||
                        _rooms.CurrentRoom.Members[0].Character != 1) return;
                    File.WriteAllText(PathFor("command"), "join" + _round); _step++; break;
                case 3:
                    if (_rooms.CurrentRoom.Members.Length != 2 || guest != "joined" + _round) return;
                    if (!SelectionsMatchMembers()) return;
                    if (_selected[_network.ClientManager.Connection.ClientId] != "CharacterB")
                        throw new Exception("Guest join reset host's manual choice");
                    if (_round == 0) { File.WriteAllText(PathFor("command"), "leaveLobby"); _step = 4; }
                    else if (_round == 1) { ClickExit(false); _step = 10; }
                    else { _rooms.SetReady(true); File.WriteAllText(PathFor("command"), "ready" + _round); _step = 6; }
                    break;
                case 4:
                    if (guest != "leftLobby") return;
                    if (_selected.Count != 1 || !SelectionsMatchMembers()) return;
                    if (!InRoom || !Lobby || !_network.ServerManager.Started || _rooms.CurrentRoom.Members.Length != 1)
                        throw new Exception("Guest lobby exit damaged host room");
                    File.WriteAllText(PathFor("command"), "joinAgain"); _step++; break;
                case 5:
                    if (_rooms.CurrentRoom.Members.Length != 2 || guest != "joinedAgain") return;
                    if (!SelectionsMatchMembers()) return;
                    if (!_abruptChecked)
                    {
                        _abruptChecked = true;
                        File.WriteAllText(PathFor("command"), "dropConnection"); _step = 12; break;
                    }
                    _rooms.SetReady(true); File.WriteAllText(PathFor("command"), "ready0"); _step++; break;
                case 6:
                    if (!_rooms.CurrentRoom.Members.All(member => member.IsReady)) return;
                    _rooms.StartRoom(); _step++; break;
                case 7:
                    if (!Game || guest != "game" + _round) return;
                    if (_round == 0) File.WriteAllText(PathFor("command"), "leaveGame");
                    else ClickExit(true);
                    _step = _round == 0 ? 8 : 10; break;
                case 8:
                    if (guest != "leftGame") return;
                    if (_selected.Count != 1 || !SelectionsMatchMembers()) return;
                    if (!InRoom || !Game || !_network.ServerManager.Started || _rooms.CurrentRoom.Members.Length != 1)
                        throw new Exception("Guest game exit damaged host room");
                    ClickExit(true); _step++; break;
                case 9:
                    if (!_session.IsIdle || !FullyStopped || !Browser) return;
                    _round = 1; _step = 1; break;
                case 10:
                    if (!_session.IsIdle || !FullyStopped || !Browser || guest != "dissolved" + _round) return;
                    if (_rooms.HasRoom) throw new Exception("Room retained after dissolve");
                    if (_round == 1) { _round = 2; _step = 1; break; }
                    File.WriteAllText(PathFor("command"), "cancelCheck"); _step++; break;
                case 11:
                    if (guest != "cancelled") return;
                    Finish("PASS: server default C registered for host/guest; lists and RoomInfo agree; host manual B survives guest join; lobby/game exit and abrupt disconnect remove selection; rejoin initializes C; connection lifecycle, dissolve and cancel work");
                    break;
                case 12:
                    if (guest != "dropped" || _rooms.CurrentRoom.Members.Length != 1 || _selected.Count != 1 || !SelectionsMatchMembers()) return;
                    File.WriteAllText(PathFor("command"), "joinAfterDrop"); _step++; break;
                case 13:
                    if (guest != "joinedAfterDrop" || !SelectionsMatchMembers() || _rooms.CurrentRoom.Members.Length != 2) return;
                    _rooms.SetReady(true); File.WriteAllText(PathFor("command"), "ready0"); _step = 6; break;
            }
        }

        private static void GuestTick()
        {
            string command = File.ReadAllText(PathFor("command"));
            if (_step == 0)
            {
                if (!FullyStopped || !_session.IsIdle) throw new Exception("Guest browser connected before join");
                _step = 1;
            }
            if (command != _lastCommand)
            {
                _lastCommand = command;
                if (command.StartsWith("join"))
                {
                    if (!FullyStopped || !_session.IsIdle) throw new Exception("Previous guest session not stopped");
                    SetBrowserInput("Port", TestPort.ToString());
                    ClickBrowser("Client");
                }
                else if (command == "leaveLobby") ClickExit(false);
                else if (command == "leaveGame") ClickExit(true);
                else if (command == "dropConnection") _network.ClientManager.StopConnection();
                else if (command.StartsWith("ready")) _rooms.SetReady(true);
                else if (command == "cancelCheck")
                {
                    _session.JoinHost("127.0.0.1", TestPort == ushort.MaxValue ? (ushort)60000 : (ushort)(TestPort + 1));
                    _session.Cancel();
                }
            }
            if (_network.ServerManager.Started) throw new Exception("Join started server on guest");
            if (InRoom && Lobby && command.StartsWith("join") && SelectionsMatchMembers() &&
                _selected.TryGetValue(_network.ClientManager.Connection.ClientId, out string ownChoice) && ownChoice == "CharacterC")
                File.WriteAllText(PathFor("guest"), command == "joinAgain" ? "joinedAgain" :
                    command == "joinAfterDrop" ? "joinedAfterDrop" : "joined" + command.Substring(4));
            if (InRoom && Game && command.StartsWith("ready"))
                File.WriteAllText(PathFor("guest"), "game" + command.Substring(5));
            if (FullyStopped && _session.IsIdle && Browser && !_rooms.HasRoom)
            {
                if (command == "leaveLobby") File.WriteAllText(PathFor("guest"), "leftLobby");
                if (command == "leaveGame") File.WriteAllText(PathFor("guest"), "leftGame");
                if (command == "dropConnection") File.WriteAllText(PathFor("guest"), "dropped");
                if (command == "ready1" || command == "join1") File.WriteAllText(PathFor("guest"), "dissolved1");
                if (command == "ready2") File.WriteAllText(PathFor("guest"), "dissolved2");
                if (command == "cancelCheck") File.WriteAllText(PathFor("guest"), "cancelled");
            }
        }

        private static bool SelectionsMatchMembers()
        {
            var members = _rooms.CurrentRoom.Members;
            return members != null && _selected.Count == members.Length && members.All(member =>
                _selected.TryGetValue(member.ClientId, out string choice) && choice == "Character" + (char)('A' + member.Character));
        }

        private static void SetBrowserInput(string name, string value)
        {
            var browser = UnityEngine.Object.FindAnyObjectByType<RoomConnectionBrowserUI>();
            browser.GetComponentsInChildren<InputField>(true).Single(input => input.name == name).text = value;
        }

        private static void ClickBrowser(string name)
        {
            var browser = UnityEngine.Object.FindAnyObjectByType<RoomConnectionBrowserUI>();
            var button = browser.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
            if (!button.interactable || !button.gameObject.activeInHierarchy) throw new Exception(name + " not available");
            button.onClick.Invoke();
        }

        private static void ClickExit(bool ingame)
        {
            var buttons = ingame ? UnityEngine.Object.FindAnyObjectByType<IngameRoomUI>().GetComponentsInChildren<Button>(true)
                : UnityEngine.Object.FindAnyObjectByType<RoomLobbyUI>().GetComponentsInChildren<Button>(true);
            buttons.Single(button => button.name == "LeaveRoom").onClick.Invoke();
            if (ingame) buttons.Single(button => button.name == "ConfirmLeave").onClick.Invoke();
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(PathFor("result"), result);
            File.Delete(PathFor("running"));
            Debug.Log("[RoomLifecycleCheck] " + result);
            EditorApplication.ExitPlaymode();
        }
    }
}
