using FishNet.Object;

namespace NCG.Player.Spawn
{
    /// <summary>ネットワークオブジェクトを生成する機能を持つインターフェース</summary>
    public interface INetworkObjectSpawnable
    {
        /// <summary>オブジェクトを生成するメソッド</summary>
        /// <param name="prefab">生成するプレハブ</param>
        /// <returns>生成した情報</returns>
        NetworkObject Spawn(NetworkObject prefab);
    }
}
