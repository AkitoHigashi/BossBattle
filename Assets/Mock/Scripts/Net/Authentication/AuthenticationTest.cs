using UnityEngine;

/// <summary>
///     認証処理のテスト用クラス
/// </summary>
public class AuthenticationTest : MonoBehaviour
{
    /// <summary>
    ///     Developer Authentication Toolのアドレス
    /// </summary>
    private const string DeveloperAddress = "localhost:6547";
    private IAuthenticationService _authenticationService;

    private void Awake()
    {
        //_authenticationService = new DeviceIdAuthenticationService("TestUser");

        string credentialName = GetCredentialName();

        _authenticationService = new DeveloperAuthenticationService(
            DeveloperAddress,
            credentialName
        );

        Debug.Log($"使用するDAT認証情報: {credentialName}");
        Debug.Log($"DAT認証設定: Address={DeveloperAddress}, Credential={credentialName}");
    }

    private async void Start()
    {
        try
        {
            bool success = await _authenticationService.LoginAsync();

            if (!success)
            {
                Debug.LogError("認証テスト失敗");
                return;
            }

            Debug.Log("認証テスト成功");

            P2PConnectionTest connectionTest = GetComponent<P2PConnectionTest>();

            if (connectionTest == null)
            {
                Debug.LogError("P2PConnectionTestコンポーネントが見つかりません。");
                return;
            }

#if UNITY_EDITOR
            // エディタで実行中の場合は、ホストとして接続を開始する
            if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
            {
                connectionTest.StartHost();
            }
            else
            {
                // Virtual Playerはクライアントとして接続を開始する
                connectionTest.StartClient();
            }
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    ///     コマンドライン引数から使用する認証情報の名前を取得する
    /// </summary>
    /// <returns> 使用する認証情報の名前 </returns>
    private string GetCredentialName()
    {
#if UNITY_EDITOR
        // メインEditorの場合はアカウントAを使用する
        if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
        {
            return "YAMADA_A";
        }

        // Virtual Playerの場合はアカウントBを使用する
        return "YAMADA_B";
#else
    return "YAMADA_A";
#endif
    }
}
