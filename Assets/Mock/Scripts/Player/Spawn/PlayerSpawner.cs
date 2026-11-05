using FishNet.Object;
using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーキャラクターの生成を管理するクラス</summary>
    public class PlayerSpawner : MonoBehaviour, INetworkObjectSpawnable
    {
        /// <summary>
        /// プレイヤーキャラクターの生成をするメソッド
        /// </summary>
        /// <param name="prefab">キャラクターのプレハブ</param>
        /// <returns>生成したオブジェクトの情報</returns>
        public NetworkObject Spawn(NetworkObject prefab)
        {
            return Instantiate(prefab);
        }
    }
}
