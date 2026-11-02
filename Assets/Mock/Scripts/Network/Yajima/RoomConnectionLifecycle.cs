using System;
using System.Collections;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima
{
    /// <summary>1ホスト1部屋。部屋の寿命とFishNet接続の寿命を一致させる。</summary>
    [DisallowMultipleComponent]
    public sealed class RoomConnectionLifecycle : MonoBehaviour
    {
        public enum ConnectionPhase { Idle, StartingHost, Connecting, Joining, InRoom, Leaving, Stopping }
        public ConnectionPhase Phase { get; private set; }
        public bool IsHost { get; private set; }
        public string Status { get; private set; } = "部屋を作成するか、接続先IPを指定して参加してください。";
        public bool IsIdle => Phase == ConnectionPhase.Idle && (_navigation == null || !_navigation.IsNavigating);
        public event Action Changed;
        private NetworkManager _network;
        private RoomClient _rooms;
        private RoomConnectionSceneNavigation _navigation;
        private string _roomName, _targetRoomId;
        private int _capacity;
        private ushort _port;
        private float _deadline;
        private bool _joinSent, _shutdownQueued, _subscribed;

        private void Awake()
        {
            _network = GetComponent<NetworkManager>();
            _rooms = GetComponent<RoomClient>();
            _navigation = GetComponent<RoomConnectionSceneNavigation>();
        }

        private void Start()
        {
            _network.ServerManager.OnServerConnectionState += ServerState;
            _network.ClientManager.OnClientConnectionState += ClientState;
            _network.ClientManager.OnAuthenticated += Authenticated;
            _rooms.StateChanged += RoomState;
            _rooms.RequestCompleted += Reply;
            _rooms.RoomsChanged += RoomList;
            UnitySceneManager.sceneLoaded += SceneLoaded;
            _subscribed = true;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == RoomSceneTransition.LobbyPath) StartCoroutine(BindLobbyExit(scene));
        }

        private IEnumerator BindLobbyExit(Scene scene)
        {
            yield return null; // RoomLobbyUI.Startのリスナー登録が完了してから置き換える。
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    if (button.name == "LeaveRoom")
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(LeaveOrDissolve);
                        var label = button.GetComponentInChildren<Text>();
                        label.text = IsHost ? "DISSOLVE ROOM" : "LEAVE ROOM";
                    }
        }

        public void CreateAndHost(string roomName, int capacity, ushort port)
        {
            if (!CanBegin()) return;
            if (string.IsNullOrWhiteSpace(roomName) || roomName.Trim().Length > 48 || capacity < 1 || capacity > 16 || port == 0)
            { SetStatus("部屋名は1〜48文字、定員は1〜16人、ポートは1〜65535で指定してください。"); return; }
            IsHost = true;
            _roomName = roomName.Trim(); _capacity = capacity; _port = port;
            _network.TransportManager.Transport.SetPort(port);
            Begin(ConnectionPhase.StartingHost, "ホストを起動しています…");
            if (!_network.ServerManager.StartConnection(port)) Fail("サーバーを起動できません。ポートの使用状況を確認してください。");
        }

        public void JoinHost(string address, ushort port, string roomId = null)
        {
            if (!CanBegin()) return;
            if (string.IsNullOrWhiteSpace(address) || port == 0)
            { SetStatus("接続先IPとポートを指定してください。"); return; }
            IsHost = false; _targetRoomId = roomId; _joinSent = false;
            _network.TransportManager.Transport.SetPort(port);
            Begin(ConnectionPhase.Connecting, "ホストへ接続しています…");
            if (!_network.ClientManager.StartConnection(address.Trim(), port)) Fail("ホストへ接続できません。");
        }

        private bool CanBegin()
        {
            if (!_subscribed || !IsIdle || _shutdownQueued) return false;
            if (_network.TransportManager.Transport.GetConnectionState(false) != LocalConnectionState.Stopped ||
                _network.TransportManager.Transport.GetConnectionState(true) != LocalConnectionState.Stopped)
            { SetStatus("前の接続が残っています。先に切断してください。"); return false; }
            return true;
        }

        public void LeaveOrDissolve()
        {
            if (Phase != ConnectionPhase.InRoom || !_rooms.HasRoom) return;
            if (IsHost)
            {
                // オーナーを別参加者へ移してから停止しない。部屋自体をサーバーごと終了する。
                StopSession("部屋を解体しています…");
                return;
            }
            Begin(ConnectionPhase.Leaving, "部屋から退出しています…");
            _rooms.LeaveRoom();
        }

        public void Cancel() => StopSession("接続をキャンセルしました。");

        private void Update()
        {
            if ((Phase == ConnectionPhase.StartingHost || Phase == ConnectionPhase.Connecting ||
                 Phase == ConnectionPhase.Joining || Phase == ConnectionPhase.Leaving) && Time.unscaledTime > _deadline)
                Fail("接続または部屋操作がタイムアウトしました。");
        }

        private void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && IsHost && Phase == ConnectionPhase.StartingHost)
            {
                Begin(ConnectionPhase.Connecting, "ホスト自身を接続しています…");
                if (!_network.ClientManager.StartConnection("127.0.0.1", _port)) Fail("ホスト自身の接続に失敗しました。");
            }
            if (args.ConnectionState == LocalConnectionState.Stopped && IsHost)
            {
                if (Phase != ConnectionPhase.Idle && Phase != ConnectionPhase.Stopping)
                    StopSession("サーバーが停止しました。");
                TryFinishStop();
            }
        }

        private void Authenticated()
        {
            if (Phase != ConnectionPhase.Connecting) return;
            Begin(ConnectionPhase.Joining, IsHost ? "部屋を作成しています…" : "参加先の部屋を確認しています…");
            if (IsHost) _rooms.CreateRoom(_roomName, _capacity);
            else _rooms.RefreshRooms();
        }

        private void RoomList(RoomInfo[] rooms)
        {
            if (IsHost || Phase != ConnectionPhase.Joining || _joinSent) return;
            RoomInfo? candidate = null;
            foreach (RoomInfo room in rooms ?? Array.Empty<RoomInfo>())
            {
                if (!string.IsNullOrEmpty(_targetRoomId) && room.Id != _targetRoomId) continue;
                if (room.HasStarted || room.Members == null || room.Members.Length >= room.Capacity) continue;
                if (candidate.HasValue) { Fail("接続先に複数の部屋があります。1ホスト1部屋の構成を使用してください。"); return; }
                candidate = room;
            }
            if (!candidate.HasValue) { Fail("参加可能な部屋がありません。ホストが部屋を作成しているか確認してください。"); return; }
            _joinSent = true;
            _rooms.JoinRoom(candidate.Value.Id);
        }

        private void RoomState()
        {
            if (_rooms.HasRoom)
            {
                if (Phase == ConnectionPhase.Joining)
                { Phase = ConnectionPhase.InRoom; SetStatus(IsHost ? "部屋を作成しました。" : "部屋に参加しました。"); }
                return;
            }
            // 既存のロビーUIがRoomClient.LeaveRoomを直接呼んだ場合も接続を終了する。
            if (Phase == ConnectionPhase.InRoom || Phase == ConnectionPhase.Leaving)
                StopSession(IsHost ? "部屋を解体しました。" : "部屋から退出しました。");
        }

        private void Reply(RoomReply reply)
        {
            if (reply.Success) return;
            if ((Phase == ConnectionPhase.Joining && (reply.Operation == RoomOperation.Create || reply.Operation == RoomOperation.Join || reply.Operation == RoomOperation.List)) ||
                (Phase == ConnectionPhase.Leaving && reply.Operation == RoomOperation.Leave))
                Fail(reply.Error ?? "部屋操作に失敗しました。");
        }

        private void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            if (Phase != ConnectionPhase.Idle && Phase != ConnectionPhase.Stopping)
                StopSession(IsHost ? "ホストの接続が終了しました。" : "ホストとの接続が終了しました。");
            TryFinishStop();
        }

        private void Begin(ConnectionPhase phase, string status)
        {
            Phase = phase; _deadline = Time.unscaledTime + 15f; SetStatus(status);
        }

        private void Fail(string error) => StopSession(error);

        private void StopSession(string status)
        {
            Phase = ConnectionPhase.Stopping;
            SetStatus(status);
            if (_shutdownQueued) return;
            _shutdownQueued = true;
            // 接続イベント・RoomClientのBroadcast受信コールバックの再入を避ける。
            StartCoroutine(StopNextFrame());
        }

        private IEnumerator StopNextFrame()
        {
            yield return null;
            _network.ClientManager.StopConnection();
            if (IsHost) _network.ServerManager.StopConnection(true);
            _shutdownQueued = false;
            TryFinishStop();
        }

        private void TryFinishStop()
        {
            if (Phase != ConnectionPhase.Stopping || _shutdownQueued ||
                _network.TransportManager.Transport.GetConnectionState(false) != LocalConnectionState.Stopped ||
                (IsHost && _network.TransportManager.Transport.GetConnectionState(true) != LocalConnectionState.Stopped)) return;
            Phase = ConnectionPhase.Idle; IsHost = false; _joinSent = false;
            Changed?.Invoke();
            _navigation?.ReturnToBrowser();
        }

        private void SetStatus(string status) { Status = status; Changed?.Invoke(); }

        private void OnDestroy()
        {
            if (!_subscribed || _network == null) return;
            _network.ServerManager.OnServerConnectionState -= ServerState;
            _network.ClientManager.OnClientConnectionState -= ClientState;
            _network.ClientManager.OnAuthenticated -= Authenticated;
            _rooms.StateChanged -= RoomState;
            _rooms.RequestCompleted -= Reply;
            _rooms.RoomsChanged -= RoomList;
            UnitySceneManager.sceneLoaded -= SceneLoaded;
        }
    }
}
