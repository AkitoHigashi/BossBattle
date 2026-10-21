using Epic.OnlineServices;
using System.Threading.Tasks;

/// <summary>
///     認証処理の共通インターフェイス
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    ///    ローカルのProductUserId
    /// </summary>
    ProductUserId LocalProductUserId { get; }

    /// <summary>
    ///    認証済みかどうか
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    ///     認証処理を行う
    /// </summary>
    /// <returns> 認証に成功したかどうか </returns>
    Task<bool> LoginAsync();
}
