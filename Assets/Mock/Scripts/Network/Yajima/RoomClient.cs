using System;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace NetworkTest_Yajima
{
    /// <summary>
    /// UIやゲームコードから使う窓口。RoomManagerとともにNetworkManagerへ配置し参照を設定。
    /// FishNetで接続・認証完了後に以下を呼び出してください。
    /// 例: client.RoomsChanged += rooms => { /* 一覧UI更新 */ };
    ///     client.StateChanged += () => { /* CurrentRoomでメンバーUI更新 */ };
    ///     client.RequestCompleted += reply => Debug.Log(reply.Success ? "成功" : reply.Error);
    ///     client.CreateRoom("テスト", 4); // 他端末はRefreshRooms() → JoinRoom(rooms[0].Id)
    ///     client.SetReady(true); // 全員Ready後、所有者がStartRoom()
    ///     client.LeaveRoom();
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomClient : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        public bool HasRoom { get; private set; }
        public RoomInfo CurrentRoom { get; private set; }
        public event Action<RoomInfo[]> RoomsChanged;
        public event Action StateChanged;
        public event Action<RoomReply> RequestCompleted;
        private bool _registered;

        private void Start()
        {
            if (_networkManager == null) _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null) throw new InvalidOperationException("RoomClientにNetworkManagerを設定してください。");
            _networkManager.ClientManager.RegisterBroadcast<RoomList>(OnList);
            _networkManager.ClientManager.RegisterBroadcast<RoomState>(OnState);
            _networkManager.ClientManager.RegisterBroadcast<RoomReply>(OnReply);
            _networkManager.ClientManager.OnClientConnectionState += OnConnectionState;
            _registered = true;
        }

        private void OnDestroy()
        {
            if (!_registered || _networkManager == null) return;
            _networkManager.ClientManager.UnregisterBroadcast<RoomList>(OnList);
            _networkManager.ClientManager.UnregisterBroadcast<RoomState>(OnState);
            _networkManager.ClientManager.UnregisterBroadcast<RoomReply>(OnReply);
            _networkManager.ClientManager.OnClientConnectionState -= OnConnectionState;
        }

        public void RefreshRooms() => Send(new RoomRequest { Operation = RoomOperation.List });
        public void CreateRoom(string roomName, int capacity) => Send(new RoomRequest { Operation = RoomOperation.Create, Name = roomName, Capacity = capacity });
        public void JoinRoom(string roomId) => Send(new RoomRequest { Operation = RoomOperation.Join, RoomId = roomId });
        public void LeaveRoom() => Send(new RoomRequest { Operation = RoomOperation.Leave });
        public void SetReady(bool ready) => Send(new RoomRequest { Operation = RoomOperation.SetReady, Ready = ready });
        public void StartRoom() => Send(new RoomRequest { Operation = RoomOperation.Start });

        private void Send(RoomRequest request)
        {
            if (!_registered || !_networkManager.ClientManager.Started || !_networkManager.ClientManager.Connection.IsAuthenticated)
            {
                RequestCompleted?.Invoke(new RoomReply { Operation = request.Operation, Success = false, Error = "FishNetへの接続・認証が完了していません。" });
                return;
            }
            _networkManager.ClientManager.Broadcast(request);
        }

        private void OnList(RoomList message, Channel channel) => RoomsChanged?.Invoke(message.Rooms);
        private void OnReply(RoomReply message, Channel channel) => RequestCompleted?.Invoke(message);
        private void OnState(RoomState message, Channel channel)
        {
            HasRoom = message.HasRoom;
            CurrentRoom = message.HasRoom ? message.Room : default;
            StateChanged?.Invoke();
        }

        private void OnConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            HasRoom = false;
            CurrentRoom = default;
            RoomsChanged?.Invoke(Array.Empty<RoomInfo>());
            StateChanged?.Invoke();
        }
    }
}
