using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AttackAnimationEvents : MonoBehaviour
{
    /// <summary>
    /// PrepareAttack から終了・中断までの攻撃イベント受付状態。
    /// Animator の再生中フラグではなく、攻撃判定が開いているかとも別。
    /// </summary>
    public bool IsPlaying { get; private set; } = false;
    /// <summary>
    /// 攻撃判定が開いた瞬間（OnHitBegin）に通知する。
    /// 剣を振った瞬間に飛び道具を出すなど、アニメーションのタイミングに合わせたい処理が購読する。
    /// </summary>
    public event Action HitStarted;
    /// <summary>
    /// 攻撃判定が閉じた瞬間（OnHitEnd、または OnHitEnd のないアニメーションでは OnAttackFinished）に通知する。
    /// 「振り終わった」タイミングで何かしたい処理（連撃の再開受付など）が購読する。
    /// </summary>
    public event Action HitEnded;
    private AttackHitbox _hitbox;
    private AttackData _attackData;
    private IReadOnlyCollection<IDamageable> _excludedTargets;

    public void Init(AttackHitbox hitbox)
    {
        _hitbox = hitbox ?? throw new System.ArgumentNullException(nameof(hitbox));
    }

    /// <summary>
    /// 新しい攻撃の開始前に前回の命中状態をリセットする。
    /// </summary>
    /// <param name="attackData">この攻撃が命中したときに与える内容。</param>
    /// <param name="excludedTargets">この攻撃では当てない相手。null なら除外なし。</param>
    public void PrepareAttack(AttackData attackData, IReadOnlyCollection<IDamageable> excludedTargets = null)
    {
        CancelAttack();
        _attackData = attackData;
        _excludedTargets = excludedTargets;
        IsPlaying = true;
    }

    /// <summary>
    /// AnimationEvent: 攻撃が当たり始めるフレームに配置する。
    /// </summary>
    public void OnHitBegin()
    {
        if (!IsPlaying) return;
        _hitbox.BeginHit(_attackData, _excludedTargets);
        HitStarted?.Invoke();
    }

    /// <summary>
    /// AnimationEvent: 攻撃の当たり判定を閉じるフレームに配置する。
    /// </summary>
    public void OnHitEnd()
    {
        if (!IsPlaying) return;
        _hitbox.EndHit();
        HitEnded?.Invoke();
    }

    /// <summary>
    /// AnimationEvent: 攻撃の再生完了を通知し、await待機を終了させる。
    /// </summary>
    public void OnAttackFinished()
    {
        // 中断済みなら終了イベントは無視する。
        // 攻撃の識別はしないため、古いアニメーションから次の攻撃へイベントが届かないよう
        // PlayerAttackAction と Controller 側でも退出・中断遷移を管理する必要がある。
        if (!IsPlaying) return;
        _hitbox.EndHit();
        IsPlaying = false;
        HitEnded?.Invoke();
    }

    /// <summary>
    /// 中断された場合に、攻撃の当たり判定を閉じる。
    /// </summary>
    public void CancelAttack()
    {
        // 通常終了時の最終命中チェックは行わず、中断時点で判定を閉じる。
        if(_hitbox != null) _hitbox.AbortHit();
        IsPlaying = false;
    }

    private void OnDisable() => CancelAttack();
}
