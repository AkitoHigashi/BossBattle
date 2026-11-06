using System;
using System.Text;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UI;

namespace NetworkTest_Yajima
{
    /// <summary>接続テスト用の表示・入力。ルーム管理はRoomManager/RoomClientに委譲します。</summary>
    public sealed class NetTestUI : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private RoomClient _rooms;
        [SerializeField] private InputField _address, _port, _roomName, _capacity;
        [SerializeField] private Button _host, _client, _server, _disconnect, _create, _refresh, _leave, _ready, _start;
        [SerializeField] private Text _connectionStatus, _message, _members, _roomTitle, _listStatus;
        [SerializeField] private RectTransform _roomList;
        [SerializeField] private Button _roomRowTemplate;
        private bool _hostPending, _authenticated, _isReady;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private float _nextRefresh;

        private void Start()
        {
            _host.onClick.AddListener(() => Connect(true, true));
            _client.onClick.AddListener(() => Connect(false, true));
            _server.onClick.AddListener(() => Connect(true, false));
            _disconnect.onClick.AddListener(Disconnect);
            _create.onClick.AddListener(CreateRoom);
            _refresh.onClick.AddListener(_rooms.RefreshRooms);
            _leave.onClick.AddListener(_rooms.LeaveRoom);
            _ready.onClick.AddListener(() => _rooms.SetReady(!_isReady));
            _start.onClick.AddListener(_rooms.StartRoom);
            _rooms.RoomsChanged += ShowRooms;
            _rooms.StateChanged += ShowState;
            _rooms.RequestCompleted += OnReply;
            _networkManager.ClientManager.OnClientConnectionState += ClientState;
            _networkManager.ServerManager.OnServerConnectionState += ServerState;
            ShowState();
        }

        private void Update()
        {
            bool authenticated = _networkManager.ClientManager.Started && _networkManager.ClientManager.Connection.IsAuthenticated;
            if (authenticated && !_authenticated) { _rooms.RefreshRooms(); _message.text = "Connected. Create a room or select JOIN."; }
            _authenticated = authenticated;
            // 一覧には他クライアントによる作成・退出も反映します。
            if (_authenticated && Time.unscaledTime >= _nextRefresh)
            { _nextRefresh = Time.unscaledTime + 3f; _rooms.RefreshRooms(); }
            bool idle = _clientState == LocalConnectionState.Stopped && _serverState == LocalConnectionState.Stopped;
            _host.interactable = _client.interactable = _server.interactable = idle;
            _address.interactable = _port.interactable = idle;
            _disconnect.interactable = !idle;
            _create.interactable = _authenticated && !_rooms.HasRoom;
            _refresh.interactable = _authenticated;
            _leave.interactable = _authenticated && _rooms.HasRoom;
            _ready.interactable = _authenticated && _rooms.HasRoom && !_rooms.CurrentRoom.HasStarted;
            bool canStart = _ready.interactable && _rooms.CurrentRoom.OwnerId == _networkManager.ClientManager.Connection.ClientId;
            if (canStart) foreach (RoomMember member in _rooms.CurrentRoom.Members) canStart &= member.IsReady;
            _start.interactable = canStart;
            _connectionStatus.text = $"SERVER  {_serverState}     |     CLIENT  {(_authenticated ? "Authenticated" : _clientState.ToString())}";
        }

        private void Connect(bool server, bool client)
        {
            if (!ushort.TryParse(_port.text, out ushort port) || port == 0)
            { _message.text = "Port must be between 1 and 65535."; return; }
            if (client && !server && string.IsNullOrWhiteSpace(_address.text))
            { _message.text = "Enter the server IP address."; return; }
            _networkManager.TransportManager.Transport.SetPort(port);
            _message.text = "Connecting...";
            _hostPending = server && client;
            if (server)
            {
                if (!_networkManager.ServerManager.StartConnection(port))
                { _hostPending = false; _message.text = "Server could not start. Check port availability."; }
            }
            else if (!_networkManager.ClientManager.StartConnection(_address.text.Trim(), port))
                _message.text = "Client could not start.";
        }

        public void Disconnect()
        {
            _hostPending = false;
            _networkManager.ClientManager.StopConnection();
            _networkManager.ServerManager.StopConnection(true);
        }

        private void ClientState(ClientConnectionStateArgs args)
        {
            _clientState = args.ConnectionState;
            if (_clientState == LocalConnectionState.Stopped)
            { _authenticated = false; _message.text = "Client disconnected. Check server IP / port if connection failed."; }
        }

        private void ServerState(ServerConnectionStateArgs args)
        {
            _serverState = args.ConnectionState;
            if (_serverState == LocalConnectionState.Started && _hostPending)
            {
                _hostPending = false;
                if (!_networkManager.ClientManager.StartConnection("127.0.0.1")) _message.text = "Host client could not start.";
            }
            if (_serverState == LocalConnectionState.Stopped)
            { _hostPending = false; _message.text = "Server stopped. Check port availability if startup failed."; }
        }

        private void CreateRoom()
        {
            if (!int.TryParse(_capacity.text, out int capacity)) { _message.text = "Enter a room capacity (1-16)."; return; }
            _rooms.CreateRoom(_roomName.text, capacity);
        }

        private void OnReply(RoomReply reply)
        {
            if (reply.Operation == RoomOperation.List && reply.Success) return;
            _message.text = reply.Success ? $"{reply.Operation} completed." : reply.Error;
            if (reply.Success) _rooms.RefreshRooms();
        }

        private void ShowRooms(RoomInfo[] rooms)
        {
            foreach (Transform child in _roomList)
                if (child != _roomRowTemplate.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _listStatus.text = rooms.Length == 0 ? "No rooms yet. Create the first room." : $"{rooms.Length} room(s) / refreshes every 3 seconds";
            foreach (RoomInfo room in rooms)
            {
                Button row = Instantiate(_roomRowTemplate, _roomList);
                row.gameObject.name = "Room_" + room.Id;
                row.gameObject.SetActive(true);
                row.GetComponentInChildren<Text>().text = $"{room.Name}    {room.Members.Length}/{room.Capacity}    {(room.HasStarted ? "IN GAME" : "JOIN >")}";
                row.interactable = !_rooms.HasRoom && !room.HasStarted && room.Members.Length < room.Capacity;
                string id = room.Id;
                row.onClick.AddListener(() => _rooms.JoinRoom(id));
            }
        }

        private void ShowState()
        {
            _isReady = false;
            _roomTitle.text = _rooms.HasRoom ? _rooms.CurrentRoom.Name + (_rooms.CurrentRoom.HasStarted ? "  /  STARTED" : "  /  LOBBY") : "CURRENT ROOM";
            if (!_rooms.HasRoom) { _members.text = "Join or create a room to see members here."; }
            else
            {
                var text = new StringBuilder();
                foreach (RoomMember member in _rooms.CurrentRoom.Members)
                {
                    bool self = member.ClientId == _networkManager.ClientManager.Connection.ClientId;
                    if (self) _isReady = member.IsReady;
                    text.AppendLine($"Client {member.ClientId}{(self ? " (YOU)" : "")}{(member.ClientId == _rooms.CurrentRoom.OwnerId ? " [OWNER]" : "")}   {(member.IsReady ? "READY" : "WAITING")}");
                }
                _members.text = text.ToString();
                if (_rooms.CurrentRoom.HasStarted) _message.text = "Room started. Loading IngameTest...";
            }
            _ready.GetComponentInChildren<Text>().text = _isReady ? "CANCEL READY" : "READY";
        }

        private void OnDestroy()
        {
            if (_rooms != null)
            { _rooms.RoomsChanged -= ShowRooms; _rooms.StateChanged -= ShowState; _rooms.RequestCompleted -= OnReply; }
            if (_networkManager == null || !_networkManager.Initialized) return;
            _networkManager.ClientManager.OnClientConnectionState -= ClientState;
            _networkManager.ServerManager.OnServerConnectionState -= ServerState;
        }
    }
}
