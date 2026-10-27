using FishNet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーキャラクターの生成を管理するクラス</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("キャラクターのリスト")] CharacterDataList _characterDataList;

        readonly HashSet<int> _spawnedClients = new();

        /// <summary>
        /// プレイヤーを生成するメソッド
        /// </summary>
        /// <param name="playerInfo">プレイヤーの情報</param>
        public void SpawnPlayer(PlayerInfo playerInfo)
        {
            var networkConnection = playerInfo.NetworkConnection;

            // プレイヤーに対するキャラクターをすでに生成していたらreturn
            if (_spawnedClients.Contains(networkConnection.ClientId)) return;

            if (_characterDataList == null)
            {
                Debug.LogError("SOがアサインされていません");
                return;
            }

            if (!_characterDataList.TryGetPlayerPrefab(playerInfo.CharacterType, out var prefab))
            {
                Debug.LogError("プレハブを取得できませんでした");
                return;
            }

            var networkObject = Instantiate(prefab);
            InstanceFinder.ServerManager.Spawn(networkObject, networkConnection);

            // 生成に失敗したらローカルの生成を削除
            if (!networkObject.IsSpawned)
            {
                Destroy(networkObject.gameObject);
                return;
            }

            // 生成を記録
            _spawnedClients.Add(networkConnection.ClientId);
        }
    }
}
