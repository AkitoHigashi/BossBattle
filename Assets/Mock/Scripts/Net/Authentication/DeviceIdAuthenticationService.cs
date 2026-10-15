using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;
using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///     DeviceIdを使用した認証処理を行うサービス
/// </summary>
public class DeviceIdAuthenticationService : IAuthenticationService
{
    public DeviceIdAuthenticationService(string displayName)
    {
        _displayName = displayName;
    }

    /// <inheritdoc/>
    public ProductUserId LocalProductUserId { get; private set; }

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        LocalProductUserId != null &&
        LocalProductUserId.IsValid();

    private readonly string _displayName;
    private bool _deviceIdCreationAttempted;

    /// <summary>
    ///     現在進行中のログイン処理を保持するフィールド
    ///     コールバック形式の認証処理をTaskベースに変換するためのTaskCompletionSource
    /// </summary>
    private TaskCompletionSource<bool> _loginSource;

    /// <inheritdoc/>
    public Task<bool> LoginAsync()
    {
        if (IsAuthenticated)
        {
            return Task.FromResult(true);
        }

        // 認証処理中なら、既存のTaskを返す
        if (_loginSource != null)
        {
            return _loginSource.Task;
        }

        _loginSource = new TaskCompletionSource<bool>();

        try
        {
            _deviceIdCreationAttempted = false;
            StartConnectLogin();
        }
        catch (Exception exception)
        {
            // 例外が発生した場合は、ログを出力して認証失敗としてTaskを完了させる
            Debug.LogException(exception);
            CompleteLogin(false);
        }

        return _loginSource.Task;
    }

    /// <summary>
    ///     EOSのConnectログイン処理を開始する
    /// </summary>
    private void StartConnectLogin()
    {
        EOSManager.Instance.StartConnectLoginWithDeviceToken(_displayName, OnLoginComplete);
    }

    /// <summary>
    ///     EOSのログイン処理が完了したときに呼び出されるコールバック
    /// </summary>
    /// <param name="callbackInfo"> ログイン処理の結果情報 </param>
    private void OnLoginComplete(LoginCallbackInfo callbackInfo)
    {
        // ログイン処理の結果に応じて処理を分岐する
        switch (callbackInfo.ResultCode)
        {
            // EOSのログイン処理が成功した場合は、ユーザーIDを取得して認証成功としてTaskを完了させる
            case Result.Success:
                OnLoginSuccess(callbackInfo.LocalUserId);
                break;

            // EOSのログイン処理でデバイスIDが存在しない場合は、デバイスIDを作成して再度ログイン処理を行う
            case Result.NotFound:
                if (_deviceIdCreationAttempted)
                {
                    Debug.LogError($"EOSログイン失敗: {callbackInfo.ResultCode}");
                    CompleteLogin(false);
                    return;
                }

                _deviceIdCreationAttempted = true;
                CreateDeviceId();
                break;
            // EOSのログイン処理でユーザーが存在しない場合は、新規ユーザーを作成する
            case Result.InvalidUser:
                CreateUser(callbackInfo.ContinuanceToken);
                break;

            default:
                Debug.LogError($"EOSログイン失敗: {callbackInfo.ResultCode}");
                CompleteLogin(false);
                break;
        }
    }

    /// <summary>
    ///     EOSの継続トークンを使用して新規ユーザーを作成する
    /// </summary>
    /// <param name="token"> 継続トークン </param>
    private void CreateUser(ContinuanceToken token)
    {
        // ContinuanceTokenはそのユーザーを作成するために使用する一時的な情報
        if (token == null)
        {
            Debug.LogError("EOSの継続トークンがありません。");
            CompleteLogin(false);
            return;
        }

        try
        {
            // 継続トークンを使用してEOSに新規ユーザーを作成する
            EOSManager.Instance.CreateConnectUserWithContinuanceToken(
                token,
                OnCreateUserComplete
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CompleteLogin(false);
        }
    }

    /// <summary>
    ///     EOSの新規ユーザー作成処理が完了したときに呼び出されるコールバック
    /// </summary>
    /// <param name="callbackInfo"> 新規ユーザー作成処理の結果情報 </param>
    private void OnCreateUserComplete(CreateUserCallbackInfo callbackInfo)
    {
        if (callbackInfo.ResultCode == Result.Success)
        {
            OnLoginSuccess(callbackInfo.LocalUserId);
            return;
        }

        Debug.LogError($"EOSユーザー作成失敗: {callbackInfo.ResultCode}");
        CompleteLogin(false);
    }

    /// <summary>
    ///     EOSのログイン処理が成功したときに呼び出されるコールバック
    /// </summary>
    /// <param name="productUserId"> ログインに成功したユーザーのID </param>  
    private void OnLoginSuccess(ProductUserId productUserId)
    {
        if (productUserId == null || !productUserId.IsValid())
        {
            Debug.LogError("有効なPUIDを取得できませんでした。");
            CompleteLogin(false);
            return;
        }

        LocalProductUserId = productUserId;

        Debug.Log($"EOSログイン成功: {LocalProductUserId}");
        CompleteLogin(true);
    }

    /// <summary>
    ///     EOSのデバイスIDを作成する
    /// </summary>
    private void CreateDeviceId()
    {
        // EOSのConnectインターフェイスを取得する
        var connectInterface = EOSManager.Instance.GetEOSConnectInterface();

        if (connectInterface == null)
        {
            Debug.LogError("EOS Connect Interfaceが取得できませんでした。");
            CompleteLogin(false);
            return;
        }

        // CreateDeviceIdOptionsはDeviceIDを作成する際の設定情報
        var options = new CreateDeviceIdOptions
        {
            // 認証に使用するデバイスの名前を指定する
            DeviceModel = "Test-Windows"
        };

        try
        {
            connectInterface.CreateDeviceId(ref options, null, OnCreateDeviceIdComplete);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CompleteLogin(false);
        }
    }

    /// <summary>
    ///     EOSのデバイスID作成処理が完了したときに呼び出されるコールバック
    /// </summary>
    /// <param name="callbackInfo"> デバイスID作成処理の結果情報 </param>
    private void OnCreateDeviceIdComplete(ref CreateDeviceIdCallbackInfo callbackInfo)
    {
        switch (callbackInfo.ResultCode)
        {
            case Result.Success:
                Debug.Log("EOSデバイスID作成成功");
                break;
            case Result.DuplicateNotAllowed:
                Debug.Log("既存のデバイスIDが存在するため、新しいデバイスIDは作成されませんでした。");
                break;

            default:
                Debug.LogError($"EOSデバイスID作成失敗: {callbackInfo.ResultCode}");
                CompleteLogin(false);
                return;
        }

        EOSManager.Instance.StartConnectLoginWithDeviceToken(_displayName, OnLoginComplete);
    }

    /// <summary>
    ///     認証処理を完了させる
    /// </summary>
    /// <param name="success"> 認証処理が成功したかどうか </param>
    private void CompleteLogin(bool success)
    {
        var source = _loginSource;
        _loginSource = null;

        source?.TrySetResult(success);
    }
}