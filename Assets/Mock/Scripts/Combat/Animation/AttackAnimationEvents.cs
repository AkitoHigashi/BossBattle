using UnityEngine;

[DisallowMultipleComponent]
public class AttackAnimationEvents : MonoBehaviour
{
    public bool IsPlaying { get; private set; } = false;
    private AttackHitbox _hitbox;
    private int _damage;

    public void Init(AttackHitbox hitbox)
    {
        _hitbox = hitbox ?? throw new System.ArgumentNullException(nameof(hitbox));
    }

    /// <summary>
    /// 新しい攻撃の開始前に前回の命中状態をリセットする。
    /// </summary>
    /// <param name="damage"></param>
    public void PrepareAttack(int damage)
    {
        CancelAttack();
        _damage = damage;
        IsPlaying = true;
    }

    /// <summary>
    /// AnimationEvent: 攻撃が当たり始めるフレームに配置する。
    /// </summary>
    public void OnHitBegin()
    {
        if (IsPlaying) _hitbox.BeginHit(_damage);
    }

    /// <summary>
    /// AnimationEvent: 攻撃の当たり判定を閉じるフレームに配置する。
    /// </summary>
    public void OnHitEnd()
    {
        if (IsPlaying) _hitbox.EndHit();
    }

    /// <summary>
    /// AnimationEvent: 攻撃の再生完了を通知し、await待機を終了させる。
    /// </summary>
    public void OnAttackFinished()
    {
        if (!IsPlaying) return;
        _hitbox.EndHit();
        IsPlaying = false;
    }

    /// <summary>
    /// 中断された場合に、攻撃の当たり判定を閉じる。
    /// </summary>
    public void CancelAttack()
    {
        if(_hitbox != null) _hitbox.AbortHit();
        IsPlaying = false;
    }

    private void OnDisable() => CancelAttack();
}