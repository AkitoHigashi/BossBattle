using FishNet;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
///     サーバー起動後にネットワークCubeを生成するテスト用クラス
/// </summary>
public class NetworkCubeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _cubePrefab;

    private void OnEnable()
    {
        // サーバーの接続状態変更を監視する
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
    ///     サーバー起動完了時にCubeを生成する
    /// </summary>
    /// <param name="args"> サーバーの接続状態情報 </param>
    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Started)
        {
            return;
        }

        if (_cubePrefab == null)
        {
            Debug.LogError("NetworkCube Prefabが未設定です。");
            return;
        }

        // サーバー側でCubeを生成する
        GameObject cube = Instantiate(
            _cubePrefab,
            new Vector3(0f, 1f, 0f),
            Quaternion.identity
        );

        // FishNetを通して接続中のクライアントへ生成を同期する
        InstanceFinder.ServerManager.Spawn(cube);

        Debug.Log("NetworkCubeをSpawnしました。");
    }
}