
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     FishyEOSを使用したP2P接続・切断・再接続のテストを行うクラス
/// </summary>
public class P2PConnectionTest : MonoBehaviour
{
    /// <summary>
    ///     接続先となるホストのPUID
    /// </summary>
    [SerializeField] private string _hostProductUserId;

    /// <summary>
    ///     テスト用のキーボード入力を受け付ける
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            StartClient();
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            StopClient();
        }

        if (keyboard.f10Key.wasPressedThisFrame)
        {
            StopServer();
        }
    }

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
        if (InstanceFinder.ServerManager.Started)
        {
            Debug.LogWarning("サーバーは既に起動しています。");
            return;
        }

        Debug.Log("ホストの起動を開始します。");

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

        if (InstanceFinder.NetworkManager.TransportManager.Transport
            is not FishyEOS transport)
        {
            Debug.LogError("TransportがFishyEOSではありません。");
            return;
        }

        transport.RemoteProductUserId = _hostProductUserId;

        Debug.Log($"ホストへの接続を開始します: {_hostProductUserId}");

        InstanceFinder.ClientManager.StartConnection();
    }

    /// <summary>
    ///     クライアント接続を停止する
    /// </summary>
    public void StopClient()
    {
        if (!InstanceFinder.ClientManager.Started)
        {
            Debug.LogWarning("クライアントは起動していません。");
            return;
        }

        Debug.Log("Clientの停止を要求しました。");

        InstanceFinder.ClientManager.StopConnection();
    }

    /// <summary>
    ///     サーバー接続を停止する
    /// </summary>
    public void StopServer()
    {
        if (!InstanceFinder.ServerManager.Started)
        {
            Debug.LogWarning("サーバーは起動していません。");
            return;
        }

        Debug.Log("Serverの停止を要求しました。");

        InstanceFinder.ServerManager.StopConnection(true);
    }

    ///     クライアントの接続状態が変化した際に呼び出される
    /// </summary>
    /// <param name="args"> クライアントの接続状態情報 </param>
    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"Client接続状態: {args.ConnectionState}");

        if (args.ConnectionState != LocalConnectionState.Stopped)
        {
            return;
        }

        // 自分で切断した場合も通知されるため、
        // 現段階ではホスト切断と区別せずに扱う
        Debug.LogWarning("サーバーとの接続が終了しました。");
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
