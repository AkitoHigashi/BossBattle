
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

/// <summary>
///     FishyEOSを使用したP2P接続のテストを行うクラス
/// </summary>
public class P2PConnectionTest : MonoBehaviour
{
    /// <summary>
    ///     接続先となるホストのPUID
    /// </summary>
    [SerializeField] private string _hostProductUserId;

    /// <summary>
    ///     FishNetの接続状態イベントを登録する
    /// </summary>
    private void OnEnable()
    {
        InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
        InstanceFinder.ClientManager.OnAuthenticated += OnAuthenticated;
    }

    /// <summary>
    ///     FishNetの接続状態イベントを解除する
    /// </summary>
    private void OnDisable()
    {
        if (InstanceFinder.ClientManager != null)
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            InstanceFinder.ClientManager.OnAuthenticated -= OnAuthenticated;
        }

        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }
    }

    /// <summary>
    ///     ホストとしてサーバーとクライアントの接続を開始する
    /// </summary>
    public void StartHost()
    {
        Debug.Log("ホストの起動を開始します。");

        // 既にサーバーが起動している場合は、二重起動を防止する
        if (InstanceFinder.ServerManager.Started)
        {
            Debug.LogWarning("サーバーは既に起動しています。");
            return;
        }

        // ホストはサーバーとクライアントの両方を起動する
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
    }

    /// <summary>
    ///     ホストのPUIDを指定してクライアント接続を開始する
    /// </summary>
    public void StartClient()
    {
        if (string.IsNullOrWhiteSpace(_hostProductUserId))
        {
            Debug.LogError("ホストのPUIDが設定されていません。");
            return;
        }

        if (InstanceFinder.ClientManager.Started)
        {
            Debug.LogWarning("クライアントは既に起動しています。");
            return;
        }

        // FishyEOSは接続先のPUIDを指定する必要がある
        if (InstanceFinder.NetworkManager.TransportManager.Transport
            is FishyEOS transport)
        {
            transport.RemoteProductUserId = _hostProductUserId;

            Debug.Log($"ホストへの接続を開始します: {_hostProductUserId}");

            InstanceFinder.ClientManager.StartConnection();
        }
        else
        {
            Debug.LogError("TransportがFishyEOSではありません。");
        }
    }

    /// <summary>
    ///     クライアントの接続状態が変化した際に呼び出される
    /// </summary>
    /// <param name="args"> クライアントの接続状態情報 </param>
    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"Client接続状態: {args.ConnectionState}");
    }

    /// <summary>
    ///     サーバーの接続状態が変化した際に呼び出される
    /// </summary>
    /// <param name="args"> サーバーの接続状態情報 </param>
    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"Server接続状態: {args.ConnectionState}");
    }

    /// <summary>
    ///     FishNet側のクライアント認証が完了した際に呼び出される
    /// </summary>
    private void OnAuthenticated()
    {
        Debug.Log("FishNetへの接続・認証が完了しました。");
    }
}
