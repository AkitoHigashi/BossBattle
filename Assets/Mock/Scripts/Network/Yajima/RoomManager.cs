using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace NetworkTest_Yajima
{
    /// <summary>
    /// サーバー権威のルーム管理。NetworkManagerと同じ寿命のオブジェクトに配置。
    /// 外部ロビーサービスは不要。ゲーム用NetworkObjectの可視性・シーン分離は別途設定すること。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField, Min(1)] private int _maxRooms = 100;
        [SerializeField, Range(1, 128)] private int _maxMembers = 16;
        private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        private readonly Dictionary<int, Room> _membership = new Dictionary<int, Room>();
        private bool _registered;

        /// <summary>サーバーで発火。ゲーム開始・シーンロード等を購読側で行います。</summary>
        public event Action<RoomInfo> RoomStarted;

        private sealed class Room
        {
            public string Id;
            public string Name;
            public int Capacity;
            public int Owner;
            public bool Started;
            public readonly List<NetworkConnection> Members = new List<NetworkConnection>();
            public readonly HashSet<int> Ready = new HashSet<int>();
        }

        private void Start()
        {
            if (_networkManager == null) _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null) throw new InvalidOperationException("RoomManagerにNetworkManagerを設定してください。");
            _networkManager.ServerManager.RegisterBroadcast<RoomRequest>(OnRequest);
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerState;
            _registered = true;
        }

        private void OnDestroy()
        {
            if (!_registered || _networkManager == null) return;
            _networkManager.ServerManager.UnregisterBroadcast<RoomRequest>(OnRequest);
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerState;
        }

        private void OnRequest(NetworkConnection connection, RoomRequest request, Channel channel)
        {
            _membership.TryGetValue(connection.ClientId, out Room room);
            string error = null;
            switch (request.Operation)
            {
                case RoomOperation.List:
                    var list = new List<RoomInfo>();
                    foreach (Room entry in _rooms.Values) list.Add(Snapshot(entry));
                    _networkManager.ServerManager.Broadcast(connection, new RoomList { Rooms = list.ToArray() });
                    break;
                case RoomOperation.Create:
                    if (room != null) { error = "既にルームに参加しています。"; break; }
                    if (_rooms.Count >= Mathf.Max(1, _maxRooms)) { error = "ルーム数の上限です。"; break; }
                    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 48)
                    { error = "ルーム名は1〜48文字で指定してください。"; break; }
                    if (request.Capacity < 1 || request.Capacity > Mathf.Clamp(_maxMembers, 1, 128))
                    { error = "定員が範囲外です。"; break; }
                    room = new Room { Id = Guid.NewGuid().ToString("N"), Name = request.Name.Trim(), Capacity = request.Capacity, Owner = connection.ClientId };
                    room.Members.Add(connection);
                    _rooms.Add(room.Id, room);
                    _membership.Add(connection.ClientId, room);
                    Publish(room);
                    break;
                case RoomOperation.Join:
                    if (room != null) { error = "既にルームに参加しています。"; break; }
                    if (string.IsNullOrEmpty(request.RoomId) || !_rooms.TryGetValue(request.RoomId, out room))
                    { error = "ルームが見つかりません。"; break; }
                    if (room.Started || room.Members.Count >= room.Capacity)
                    { error = "開始済み、または満員です。"; break; }
                    room.Members.Add(connection);
                    _membership.Add(connection.ClientId, room);
                    Publish(room);
                    break;
                case RoomOperation.Leave:
                    if (room == null) { error = "ルームに参加していません。"; break; }
                    Remove(connection);
                    _networkManager.ServerManager.Broadcast(connection, new RoomState { HasRoom = false });
                    break;
                case RoomOperation.SetReady:
                    if (room == null || room.Started) { error = "待機中のルームに参加してください。"; break; }
                    if (request.Ready) room.Ready.Add(connection.ClientId);
                    else room.Ready.Remove(connection.ClientId);
                    Publish(room);
                    break;
                case RoomOperation.Start:
                    if (room == null || room.Owner != connection.ClientId || room.Started)
                    { error = "待機中のルーム所有者のみ開始できます。"; break; }
                    if (room.Ready.Count != room.Members.Count) { error = "全員のReadyが必要です。"; break; }
                    room.Started = true;
                    Publish(room);
                    // 応答を送ってからゲーム側へ制御を渡します。
                    _networkManager.ServerManager.Broadcast(connection, new RoomReply { Operation = request.Operation, Success = true });
                    RoomStarted?.Invoke(Snapshot(room));
                    return;
                default:
                    error = "不明な操作です。";
                    break;
            }
            _networkManager.ServerManager.Broadcast(connection, new RoomReply { Operation = request.Operation, Success = error == null, Error = error });
        }

        private void Remove(NetworkConnection connection)
        {
            if (!_membership.TryGetValue(connection.ClientId, out Room room)) return;
            _membership.Remove(connection.ClientId);
            room.Members.RemoveAll(member => member.ClientId == connection.ClientId);
            room.Ready.Remove(connection.ClientId);
            if (room.Members.Count == 0) { _rooms.Remove(room.Id); return; }
            if (room.Owner == connection.ClientId) room.Owner = room.Members[0].ClientId;
            Publish(room);
        }

        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) Remove(connection);
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            // 複数Transportの場合は他のTransportが稼働している間は保持。
            if (args.ConnectionState != LocalConnectionState.Stopped || _networkManager.ServerManager.Started) return;
            _rooms.Clear();
            _membership.Clear();
        }

        private void Publish(Room room)
        {
            var state = new RoomState { HasRoom = true, Room = Snapshot(room) };
            foreach (NetworkConnection member in room.Members) _networkManager.ServerManager.Broadcast(member, state);
        }

        private static RoomInfo Snapshot(Room room)
        {
            var members = new RoomMember[room.Members.Count];
            for (int i = 0; i < members.Length; i++)
            {
                int id = room.Members[i].ClientId;
                members[i] = new RoomMember { ClientId = id, IsReady = room.Ready.Contains(id) };
            }
            return new RoomInfo { Id = room.Id, Name = room.Name, Capacity = room.Capacity, OwnerId = room.Owner, HasStarted = room.Started, Members = members };
        }
    }
}
