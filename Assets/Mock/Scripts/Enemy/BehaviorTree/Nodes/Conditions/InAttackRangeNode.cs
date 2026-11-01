using UnityEngine;

/// <summary>
/// 攻撃が可能な範囲にターゲットがいるかを判定する条件ノード
/// </summary>
public sealed class InAttackRangeNode : ConditionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "ターゲットが攻撃範囲（Range）内にいればSuccess、いなければFailure。";

    [SerializeField]
    private SectorRangeSettings _range;

    protected override NodeStatus OnUpdate()
    {
        var owner = _blackBoard?.Transform;
        var target = _blackBoard?.Target;
        if (_range == null || owner == null || target == null)
            return NodeStatus.Failure;

        return _range.Contains(owner.position, owner.forward, target.transform.position)
            ? NodeStatus.Success
            : NodeStatus.Failure;
    }
}
