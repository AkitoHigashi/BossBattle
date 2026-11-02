using FishNet;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
///     サーバー起動時に敵を1体生成するテスト用クラス
/// </summary>
public class NetworkEnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Vector3 _spawnPosition = new Vector3(5f, 1f, 5f);

    private void OnEnable()
    {
        InstanceFinder.ServerManager.OnServerConnectionState +=
            OnServerConnectionState;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -=
                OnServerConnectionState;
        }
    }

    /// <summary>
    ///     サーバー起動時に敵を生成する
    /// </summary>
    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Started)
        {
            return;
        }

        GameObject enemy = Instantiate(
            _enemyPrefab,
            _spawnPosition,
            Quaternion.identity
        );

        InstanceFinder.ServerManager.Spawn(enemy);

        Debug.Log("NetworkEnemyをSpawnしました。");
    }
}
