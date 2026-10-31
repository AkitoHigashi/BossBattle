using FishNet.Connection;

namespace NCG.Player.Spawn
{
    /// <summary>生成したプレイヤーについてのデータを格納した構造体</summary>
    public readonly struct PlayerInfo
    {
        /// <summary>接続のアクションを行ったプレイヤーのデータ</summary>
        public readonly NetworkConnection NetworkConnection;

        public PlayerInfo(NetworkConnection networkConnection)
        {
            NetworkConnection = networkConnection;
        }
    }
}
