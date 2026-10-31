using FishNet.Broadcast;

namespace NetworkTest_Yajima
{
    public enum RoomOperation : byte { List, Create, Join, Leave, SetReady, Start, SelectCharacter }

    // Broadcast用DTO。RoomIdはサーバー内でのみ有効です。
    public struct RoomRequest : IBroadcast
    {
        public RoomOperation Operation;
        public string RoomId;
        public string Name;
        public int Capacity;
        public bool Ready;
        public int Character;
    }

    public struct RoomMember
    {
        public int ClientId;
        public bool IsReady;
        public int Character;
    }

    public struct RoomInfo
    {
        public string Id;
        public string Name;
        public int Capacity;
        public int OwnerId;
        public bool HasStarted;
        public RoomMember[] Members;
    }

    public struct RoomReply : IBroadcast
    {
        public RoomOperation Operation;
        public bool Success;
        public string Error;
    }

    public struct RoomList : IBroadcast { public RoomInfo[] Rooms; }
    // HasRoom=falseは退出または所属なし。開始後の更新もこのメッセージで通知します。
    public struct RoomState : IBroadcast { public bool HasRoom; public RoomInfo Room; }
}

