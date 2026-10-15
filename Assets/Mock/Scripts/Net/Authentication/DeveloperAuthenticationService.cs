using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using PlayEveryWare.EpicOnlineServices;
using System;
using System.Threading.Tasks;
using UnityEngine;
using AuthLoginCallbackInfo = Epic.OnlineServices.Auth.LoginCallbackInfo;
using ConnectCreateUserCallbackInfo = Epic.OnlineServices.Connect.CreateUserCallbackInfo;
using ConnectLoginCallbackInfo = Epic.OnlineServices.Connect.LoginCallbackInfo;

/// <summary>
///     Developer Authentication Toolを使用した認証処理を行うサービス
/// </summary>
public class DeveloperAuthenticationService : IAuthenticationService
{
    public DeveloperAuthenticationService(string address, string credentialName)
    {
        _address = address;
        _credentialName = credentialName;
    }

    /// <inheritdoc/>
    public ProductUserId LocalProductUserId { get; private set; }

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        LocalProductUserId != null &&
        LocalProductUserId.IsValid();

    private readonly string _address;
    private readonly string _credentialName;

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
        Task<bool> loginTask = _loginSource.Task;

        try
        {
            StartDeveloperLogin();
        }
        catch (Exception exception)
        {
            // 例外が発生した場合は、ログを出力して認証失敗としてTaskを完了させる
            Debug.LogException(exception);
            CompleteLogin(false);
        }

        return loginTask;
    }

    /// <summary>
    ///     Developer Authentication Toolを使用してEOS Authのログイン処理を開始する
    /// </summary>
    private void StartDeveloperLogin()
    {
        EOSManager.Instance.StartLoginWithLoginTypeAndToken(
            LoginCredentialType.Developer,
            _address,
            _credentialName,
            OnAuthComplete
        );
    }

    /// <summary>
    ///     EOS Authのログイン処理が完了したときに呼び出されるコールバック
    /// </summary>
    /// <param name="callbackInfo"> EOS Authのログイン処理の結果情報 </param>
    private void OnAuthComplete(AuthLoginCallbackInfo callbackInfo)
    {
        // EOS Authのログイン処理が失敗した場合は、認証失敗としてTaskを完了させる
        if (callbackInfo.ResultCode != Result.Success)
        {
            Debug.LogError($"EOS Developer認証失敗: {callbackInfo.ResultCode}");
            CompleteLogin(false);
            return;
        }

        try
        {
            // Epicアカウントの認証に成功したため、EOS Connectのログイン処理を開始する
            StartConnectLogin(callbackInfo.LocalUserId);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CompleteLogin(false);
        }
    }

    /// <summary>
    ///     Epicアカウントを使用してEOS Connectのログイン処理を開始する
    /// </summary>
    /// <param name="epicAccountId"> EOS Authで認証したEpicアカウントのID </param>
    private void StartConnectLogin(EpicAccountId epicAccountId)
    {
        EOSManager.Instance.StartConnectLoginWithEpicAccount(
            epicAccountId,
            OnConnectLoginComplete
        );
    }

    /// <summary>
    ///     EOS Connectのログイン処理が完了したときに呼び出されるコールバック
    /// </summary>
    /// <param name="callbackInfo"> EOS Connectのログイン処理の結果情報 </param>
    private void OnConnectLoginComplete(ConnectLoginCallbackInfo callbackInfo)
    {
        // EOS Connectのログイン処理の結果に応じて処理を分岐する
        switch (callbackInfo.ResultCode)
        {
            // EOS Connectへのログインに成功した場合は、PUIDを取得して認証成功としてTaskを完了させる
            case Result.Success:
                OnLoginSuccess(callbackInfo.LocalUserId);
                break;

            // EOS Connectに対応するユーザーが存在しない場合は、新規ユーザーを作成する
            case Result.InvalidUser:
                CreateUser(callbackInfo.ContinuanceToken);
                break;

            default:
                Debug.LogError($"EOS Connectログイン失敗: {callbackInfo.ResultCode}");
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
    private void OnCreateUserComplete(ConnectCreateUserCallbackInfo callbackInfo)
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

        Debug.Log($"EOS Developer認証成功 [{_credentialName}]: {LocalProductUserId}");
        CompleteLogin(true);
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
