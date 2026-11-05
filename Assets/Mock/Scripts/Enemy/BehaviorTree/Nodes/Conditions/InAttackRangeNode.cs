using UnityEngine;

/// <summary>
/// 攻撃が可能な範囲にターゲットがいるかを判定する条件ノード
/// </summary>
public sealed class InAttackRangeNode : ConditionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "ターゲットが攻撃範囲（Range）内にいればSuccess、いなければFailure。";

    [SerializeField, Tooltip("攻撃範囲の半径")]
    private float _attackRange = 5f;
    protected override NodeStatus OnUpdate()
    {
        if (_blackBoard.Target == null || _blackBoard.Transform == null)
            return NodeStatus.Failure;

        return Vector3.Distance(_blackBoard.Transform.position, _blackBoard.Target.transform.position) < _attackRange
            ? NodeStatus.Success
            : NodeStatus.Failure;
    }
}
