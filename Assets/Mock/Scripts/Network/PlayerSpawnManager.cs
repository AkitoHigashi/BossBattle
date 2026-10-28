using FishNet;
using FishNet.Managing.Scened;
using System.Collections.Generic;
using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーの生成に関するクラス</summary>
    public sealed class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField, Tooltip("キャラクターのリスト")] CharacterDataList _characterDataList;
        PlayerSpawner _spawner;
        readonly HashSet<int> _spawnedClients = new();

        private void Awake()
        {
            _spawner = GetComponentInChildren<PlayerSpawner>();
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        }

        /// <summary>
        /// クライアントの参加シーンが変わった時に呼ばれるメソッド
        /// </summary>
        /// <param name="args">クライアントの情報</param>
        void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            // シーンから退出した場合はreturn
            if (!args.Added) return;

            // 参加したシーンがこのゲームオブジェクトとは異なったらreturn
            if (args.Scene != gameObject.scene) return;

            // サーバーが立ち上がっていなければ（自分がサーバーじゃなければ）return
            if (!InstanceFinder.ServerManager.Started) return;

            // プレイヤー情報の生成
            var playerInfo = new PlayerInfo(args.Connection, CharacterType.CharacterA);
            SpawnPlayer(playerInfo);
        }

        /// <summary>
        /// プレイヤーの生成を同期するメソッド
        /// </summary>
        /// <param name="playerInfo">プレイヤーの情報</param>
        void SpawnPlayer(PlayerInfo playerInfo)
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

            // イベント経由でプレイヤーキャラクターの生成
            var networkObject = _spawner.Spawn(prefab);

            // 生成同期と操作主の登録
            InstanceFinder.ServerManager.Spawn(networkObject, networkConnection, gameObject.scene);

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
