/// <summary>Time To Attackの後に対象へダメージを与え、硬直が終わるまでRunningを返す。</summary>
public sealed class NormalAttackNode : AttackNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Time To Attackだけ待ってからターゲットにダメージ（基本攻撃力×倍率）を与え、Recovery Timeの間待ってSuccess。\nWarning Areaを設定すると待っている間に予兆を描画し、範囲内の時だけ当たる。ターゲットがいない・倒れている場合はFailure。";

    protected override NodeStatus OnAttack()
    {
        var target = _blackBoard?.Target;
        if (target == null || !target.TryGetComponent(out CharacterComposition composition) ||
            composition.Character == null || composition.Character.Health.CurrentValue <= 0)
            return NodeStatus.Failure;

        Hit();
        return NodeStatus.Success;
    }
}
