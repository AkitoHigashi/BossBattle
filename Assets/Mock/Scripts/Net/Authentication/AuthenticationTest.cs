using UnityEngine;

/// <summary>
///     認証処理のテスト用クラス
/// </summary>
public class AuthenticationTest : MonoBehaviour
{
    private IAuthenticationService _authenticationService;

    private void Awake()
    {
        _authenticationService = new DeviceIdAuthenticationService("TestUser");
    }

    private async void Start()
    {
        try
        {
            bool success = await _authenticationService.LoginAsync();

            if (!success)
            {
                Debug.LogError("認証テスト失敗");

            }
            else
            {
                Debug.Log("認証テスト成功");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
