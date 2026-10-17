using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace NetworkTest_Yajima
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] Player _player;
        [SerializeField] Transform[] _spawnPos;
        private readonly HashSet<int> _spawnedClients = new();
        private int _nextSpawn;

        private void Awake()
        {
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.ServerManager == null) return;
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        }

        private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            // この通知はサーバー側で、対象クライアントのシーン所属が確定した後に届きます。
            if (!args.Added || args.Scene != gameObject.scene || !InstanceFinder.ServerManager.Started) return;
            SpawnPlayer(args.Connection);
        }

        private void SpawnPlayer(NetworkConnection client)
        {
            if (_spawnedClients.Contains(client.ClientId)) return;

            if (_player == null)
            {
                Debug.LogWarning("Playerプレハブをアサインしてください");
                return;
            }

            if (_spawnPos == null || _nextSpawn >= _spawnPos.Length || _spawnPos[_nextSpawn] == null)
            {
                Debug.LogWarning($"Client {client.ClientId}の生成位置がありません", this);
                return;
            }

            Transform spawn = _spawnPos[_nextSpawn];
            NetworkObject player = Instantiate(_player, spawn.position, spawn.rotation);

            // ServerManagerでSpawnを使える
            // (生成同期するオブジェクト, 誰に連携させるか, どのシーンに生成するか)
            InstanceFinder.ServerManager.Spawn(player, client, gameObject.scene);
            // スポーン失敗の場合サーバー上から消す
            if (!player.IsSpawned)
            {
                Destroy(player.gameObject);
                return;
            }
            _spawnedClients.Add(client.ClientId);
            _nextSpawn++;
        }
    }
}
