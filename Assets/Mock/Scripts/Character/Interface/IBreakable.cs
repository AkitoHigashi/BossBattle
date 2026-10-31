/// <summary>
/// ブレイク値を受け取れる対象（ボスなど）。
/// </summary>
/// <remarks>
/// IDamageable に追加せず別インターフェースにしたのは、ブレイクの概念がない対象
/// （プレイヤー・雑魚など）にまで実装を強制しないため。
/// AttackData.ApplyTo は、IDamageable を実装したコンポーネントがこれも実装していればブレイク値を渡す。
/// ブレイクゲージの蓄積・ブレイク状態への移行は、実装する側（敵）の責務。
/// </remarks>
public interface IBreakable
{
    /// <summary>
    /// ブレイク値を加算する。
    /// </summary>
    /// <param name="breakValue">加算するブレイク値（0より大きい）。</param>
    void TakeBreak(float breakValue);
}
