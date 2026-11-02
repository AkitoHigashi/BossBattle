using System;
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
        private static NetworkManager _network;
        private static RoomClient _rooms;
        private static RoomConnectionLifecycle _session;
        private static int _step, _round;
        private static double _deadline, _next, _wait;
        private static string _lastCommand;

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
            File.WriteAllText(PathFor("running"), "run");
            EditorSceneManager.OpenScene(RoomSceneTransition.BrowserPath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(PathFor("running"))) return;
            _step = _round = 0; _next = _wait = 0; _lastCommand = null;
            _network = null; _rooms = null; _session = null;
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
                    _session.CreateAndHost("", 4, 18535);
                    if (!FullyStopped || !_session.IsIdle) throw new Exception("Invalid create started server");
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot("Temp/RoomLifecycle.Browser.png");
                    _step++; break;
                case 1:
                    SetBrowserInput("RoomName", "Room lifecycle " + _round);
                    SetBrowserInput("Port", "18535");
                    ClickBrowser("Create"); _step++; break;
                case 2:
                    if (!InRoom || !Lobby) return;
                    if (!_session.IsHost || !_network.ServerManager.Started || !_network.ClientManager.Connection.IsAuthenticated)
                        throw new Exception("Create did not start host and room together");
                    File.WriteAllText(PathFor("command"), "join" + _round); _step++; break;
                case 3:
                    if (_rooms.CurrentRoom.Members.Length != 2 || guest != "joined" + _round) return;
                    if (_round == 0) { File.WriteAllText(PathFor("command"), "leaveLobby"); _step = 4; }
                    else if (_round == 1) { ClickExit(false); _step = 10; }
                    else { _rooms.SetReady(true); File.WriteAllText(PathFor("command"), "ready" + _round); _step = 6; }
                    break;
                case 4:
                    if (guest != "leftLobby") return;
                    if (!InRoom || !Lobby || !_network.ServerManager.Started || _rooms.CurrentRoom.Members.Length != 1)
                        throw new Exception("Guest lobby exit damaged host room");
                    File.WriteAllText(PathFor("command"), "joinAgain"); _step++; break;
                case 5:
                    if (_rooms.CurrentRoom.Members.Length != 2 || guest != "joinedAgain") return;
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
                    Finish("PASS: disconnected browser; create starts host; join connects client; lobby/game guest exits disconnect only guest; lobby/game host dissolve disconnects everyone; recreate and cancel work");
                    break;
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
                    SetBrowserInput("Port", "18535");
                    ClickBrowser("Client");
                }
                else if (command == "leaveLobby") ClickExit(false);
                else if (command == "leaveGame") ClickExit(true);
                else if (command.StartsWith("ready")) _rooms.SetReady(true);
                else if (command == "cancelCheck")
                {
                    _session.JoinHost("127.0.0.1", 18536);
                    _session.Cancel();
                }
            }
            if (_network.ServerManager.Started) throw new Exception("Join started server on guest");
            if (InRoom && Lobby && command.StartsWith("join"))
                File.WriteAllText(PathFor("guest"), command == "joinAgain" ? "joinedAgain" : "joined" + command.Substring(4));
            if (InRoom && Game && command.StartsWith("ready"))
                File.WriteAllText(PathFor("guest"), "game" + command.Substring(5));
            if (FullyStopped && _session.IsIdle && Browser && !_rooms.HasRoom)
            {
                if (command == "leaveLobby") File.WriteAllText(PathFor("guest"), "leftLobby");
                if (command == "leaveGame") File.WriteAllText(PathFor("guest"), "leftGame");
                if (command == "ready1" || command == "join1") File.WriteAllText(PathFor("guest"), "dissolved1");
                if (command == "ready2") File.WriteAllText(PathFor("guest"), "dissolved2");
                if (command == "cancelCheck") File.WriteAllText(PathFor("guest"), "cancelled");
            }
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
