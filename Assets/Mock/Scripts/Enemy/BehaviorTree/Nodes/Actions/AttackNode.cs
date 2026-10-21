using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 攻撃ノードの共通設定
/// </summary>
public abstract class AttackNode : ActionNode
{
    [FormerlySerializedAs("_damageMultiplier")]
    [SerializeField, Min(0f), Tooltip("キャラの基本攻撃力に掛ける、この攻撃固有の倍率。")]
    private float _attackMultiplier = 1.5f;

    /// <summary>整数への丸め前のダメージ。敵ごとの基本攻撃力と、攻撃ごとの倍率を組み合わせる。</summary>
    protected float Damage => _blackBoard.AttackDamage * Mathf.Max(0f, _attackMultiplier);
}
