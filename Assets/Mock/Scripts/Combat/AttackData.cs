using UnityEngine;

/// <summary>
/// 1回の命中で相手に与える内容（ダメージとブレイク値）。
/// 剣の当たり判定（AttackHitbox）と飛び道具（SkillProjectile）の両方がこの形で受け渡す。
/// </summary>
/// <remarks>
/// 値をまとめて渡すのは、項目が増えたとき（属性・ノックバックなど）に
/// 途中の受け渡し箇所すべての引数を変えずに済むようにするため。
/// readonly struct なので、受け取った側が値を書き換えて他へ影響を与えることはない。
/// </remarks>
public readonly struct AttackData
{
    /// <summary>与えるダメージ。</summary>
    public int Damage { get; }
    /// <summary>
    /// 与えるブレイク値。仕様の「ブレイク値①」は合計 1 を表す。
    /// 「3連全ヒットで①」のように複数ヒットで分け合う場合があるため小数にしている。
    /// </summary>
    public float BreakValue { get; }

    public AttackData(int damage, float breakValue)
    {
        Damage = Mathf.Max(0, damage);
        BreakValue = Mathf.Max(0f, breakValue);
    }

    /// <summary>
    /// 倍率を掛けた新しい AttackData を返す（溜め攻撃の強化などに使う）。
    /// </summary>
    public AttackData Scale(float damageMultiplier, float breakMultiplier)
    {
        return new AttackData(Mathf.RoundToInt(Damage * damageMultiplier), BreakValue * breakMultiplier);
    }

    /// <summary>
    /// 命中した相手にダメージとブレイク値を与える。
    /// 剣と飛び道具で同じ処理を使い、命中時の扱いがずれないようにしている。
    /// </summary>
    public void ApplyTo(IDamageable target)
    {
        if (target == null)
            return;

        target.TakeDamage(Damage);

        // ブレイクの仕組みを持つ相手だけに渡す。敵側が IBreakable を実装すれば自動的に届く。
        if (BreakValue > 0f && target is IBreakable breakable)
            breakable.TakeBreak(BreakValue);
    }
}
