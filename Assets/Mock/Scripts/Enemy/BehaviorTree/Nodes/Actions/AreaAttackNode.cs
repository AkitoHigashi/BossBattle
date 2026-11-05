using UnityEngine;

/// <summary>地上の予兆の後、範囲内に攻撃してエフェクトを出す</summary>
public sealed class AreaAttackNode : AttackNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Time To Attackの間、足元の地面にWarning Areaの予兆を出した後、範囲内のターゲットにダメージを与える。\nエフェクトを設定していれば発動時に生成し、Recovery Timeが過ぎたらSuccess。";

    protected override NodeStatus OnAttack()
    {
        Hit();
        return NodeStatus.Success;
    }
}
