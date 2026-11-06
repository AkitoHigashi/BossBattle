using System;
using System.IO;
using System.Linq;
using FishNet.Managing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima.Editor
{
    /// <summary>Multiplayer Play ModeのPlayer 2を使い、本人だけが退出することを両方向で確認する。</summary>
    [InitializeOnLoad]
    public static class IngameRoomExitRegression
    {
        private static readonly string Project = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        private static readonly bool Guest = Project.Contains("/Library/VP/");
        private static readonly string Root = Guest ? Project.Substring(0, Project.IndexOf("/Library/VP/", StringComparison.Ordinal)) : Project;
        private static string FilePath(string name) => Root + "/Temp/IngameExitRegression." + name;
        private static NetworkManager _network;
        private static RoomClient _rooms;
        private static RoomInfo[] _list = Array.Empty<RoomInfo>();
        private static int _step, _round;
        private static double _deadline, _next, _guestRemainSince;
        private static bool _waiting;

        static IngameRoomExitRegression()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (!Guest && File.Exists(FilePath("launch")) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(FilePath("launch"));
                Run();
            }
        }

        [MenuItem("Tools/Yajima/Check Exit With Player 2")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "command", "guest", "result" })
                File.WriteAllText(FilePath(name), "");
            File.WriteAllText(FilePath("running"), "run");
            EditorSceneManager.OpenScene(RoomSceneTransition.BrowserPath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(FilePath("running"))) return;
            _step = _round = 0;
            _waiting = false;
            _guestRemainSince = 0;
            _deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup > _deadline) throw new Exception($"Timeout {(Guest ? "guest" : "host")} step {_step}, round {_round}");
                if (EditorApplication.timeSinceStartup < _next) return;
                _next = EditorApplication.timeSinceStartup + 0.1;
                if (_network == null)
                {
                    _network = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
                    if (_network == null) return;
                    _rooms = _network.GetComponent<RoomClient>();
                    _rooms.RoomsChanged += rooms => _list = rooms;
                }
                if (Guest) GuestTick(); else HostTick();
            }
            catch (Exception error)
            {
                if (Guest) File.WriteAllText(FilePath("guest"), "FAIL: " + error);
                else Finish("FAIL: " + error);
                EditorApplication.update -= Tick;
            }
        }

        private static void HostTick()
        {
            string guest = File.ReadAllText(FilePath("guest"));
            if (guest.StartsWith("FAIL")) throw new Exception(guest);
            switch (_step)
            {
                case 0: _network.ServerManager.StartConnection(18533); _step++; break;
                case 1:
                    if (!_network.ServerManager.Started) return;
                    _network.ClientManager.StartConnection("127.0.0.1", 18533);
                    File.WriteAllText(FilePath("command"), "connect"); _step++; break;
                case 2:
                    if (!_network.ClientManager.Connection.IsAuthenticated || _network.ServerManager.Clients.Count != 2) return;
                    _rooms.CreateRoom("Exit regression " + _round, 4);
                    File.WriteAllText(FilePath("command"), "join" + _round); _step++; break;
                case 3:
                    if (!_rooms.HasRoom || _rooms.CurrentRoom.Members.Length != 2) return;
                    _rooms.SetReady(true); _step++; break;
                case 4:
                    if (!_rooms.CurrentRoom.Members.All(m => m.IsReady)) return;
                    _rooms.StartRoom(); _step++; break;
                case 5:
                    if (SceneManager.GetActiveScene().path != IngameRoomExitCoordinator.IngamePath || guest != "ingame" + _round) return;
                    if (_round == 0) File.WriteAllText(FilePath("command"), "leaveGuest");
                    else { LeaveUsingUI(); File.WriteAllText(FilePath("command"), "hostLeaving"); }
                    _step++; break;
                case 6:
                    if (_round == 0)
                    {
                        if (guest != "guestLeft") return;
                        if (!_rooms.HasRoom || _rooms.CurrentRoom.Members.Length != 1 ||
                            SceneManager.GetActiveScene().path != IngameRoomExitCoordinator.IngamePath)
                            throw new Exception("Guest exit also displaced host");
                        if (!_network.ServerManager.Started || _network.ServerManager.Clients.Count != 2)
                            throw new Exception("Guest exit disconnected another player");
                        LeaveUsingUI(); _step++; break;
                    }
                    if (guest != "guestStayed") return;
                    if (_rooms.HasRoom || SceneManager.GetActiveScene().path != RoomSceneTransition.BrowserPath) return;
                    if (!_network.ServerManager.Started || _network.ServerManager.Clients.Count != 2)
                        throw new Exception("Host exit disconnected another player");
                    Finish("PASS: guest exit keeps host ingame; host exit keeps guest ingame; both clients remain connected");
                    break;
                case 7:
                    if (_rooms.HasRoom || SceneManager.GetActiveScene().path != RoomSceneTransition.BrowserPath) return;
                    _round = 1; _step = 2; _waiting = false; break;
            }
        }

        private static void GuestTick()
        {
            string command = File.ReadAllText(FilePath("command"));
            if (_step == 0)
            {
                if (command != "connect") return;
                _network.ClientManager.StartConnection("127.0.0.1", 18533); _step = 1; return;
            }
            if (!_network.ClientManager.Connection.IsAuthenticated) return;
            if (command == "join" + _round && !_rooms.HasRoom)
            {
                if (!_waiting) { _waiting = true; _rooms.RefreshRooms(); return; }
                var room = _list.FirstOrDefault(r => r.Name == "Exit regression " + _round);
                if (string.IsNullOrEmpty(room.Id)) { _rooms.RefreshRooms(); return; }
                _rooms.JoinRoom(room.Id); _waiting = false; return;
            }
            if (_rooms.HasRoom && !_rooms.CurrentRoom.HasStarted)
            {
                var self = _rooms.CurrentRoom.Members.First(m => m.ClientId == _network.ClientManager.Connection.ClientId);
                if (!self.IsReady) _rooms.SetReady(true);
            }
            if (_rooms.HasRoom && SceneManager.GetActiveScene().path == IngameRoomExitCoordinator.IngamePath)
            {
                if (UnityEngine.Object.FindAnyObjectByType<IngameRoomUI>() == null) return;
                if (command == "leaveGuest") { LeaveUsingUI(); return; }
                if (command == "hostLeaving" && _rooms.CurrentRoom.Members.Length == 1)
                {
                    if (_rooms.CurrentRoom.Members[0].ClientId != _network.ClientManager.Connection.ClientId)
                        throw new Exception("Wrong remaining member");
                    if (_guestRemainSince == 0) _guestRemainSince = EditorApplication.timeSinceStartup;
                    if (EditorApplication.timeSinceStartup - _guestRemainSince < 2) return;
                    File.WriteAllText(FilePath("guest"), "guestStayed"); return;
                }
                File.WriteAllText(FilePath("guest"), "ingame" + _round);
            }
            if (command == "leaveGuest" && !_rooms.HasRoom && SceneManager.GetActiveScene().path == RoomSceneTransition.BrowserPath)
            {
                File.WriteAllText(FilePath("guest"), "guestLeft");
                _round = 1; _list = Array.Empty<RoomInfo>();
            }
        }

        private static void LeaveUsingUI()
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<IngameRoomUI>();
            var buttons = ui.GetComponentsInChildren<Button>(true);
            buttons.Single(b => b.name == "LeaveRoom").onClick.Invoke();
            buttons.Single(b => b.name == "ConfirmLeave").onClick.Invoke();
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(FilePath("result"), result);
            File.Delete(FilePath("running"));
            Debug.Log("[IngameExitRegression] " + result);
            EditorApplication.ExitPlaymode();
        }
    }
}
