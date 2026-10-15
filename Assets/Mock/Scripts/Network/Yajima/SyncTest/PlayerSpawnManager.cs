using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;

namespace NetworkTest_Yajima
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] Player _player;
        [SerializeField] Transform[] _spawnPos;
        private NetworkManager _networkManager;
        private readonly HashSet<int> _spawnedClients = new();
        private int _nextSpawn;

        private void Awake()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogWarning("NetworkManagerが見つかりません", this);
                return;
            }

            _networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        }

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.Initialized)
                _networkManager.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        }

        private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            // この通知はサーバー側で、対象クライアントのシーン所属が確定した後に届きます。
            if (!args.Added || args.Scene != gameObject.scene || !_networkManager.ServerManager.Started) return;
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
            Player player = Instantiate(_player, spawn.position, spawn.rotation);
            // 読み込み中はActiveSceneがIngameTestとは限らないため、生成先を明示します。
            _networkManager.ServerManager.Spawn(player.NetworkObject, client, gameObject.scene);
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
