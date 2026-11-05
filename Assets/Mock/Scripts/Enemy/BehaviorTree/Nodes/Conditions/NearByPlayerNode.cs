using UnityEngine;
public class NearByPlayerNode : ConditionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "ターゲットがDetect Distance以内にいればSuccess、範囲外ならFailure。";

    protected override NodeStatus OnUpdate()
    {
        if (_blackBoard?.Target == null || _blackBoard.Transform == null)
        {
            return NodeStatus.Failure;
        }

        return Vector3.Distance(_blackBoard.Target.transform.position, _blackBoard.Transform.position) < _detectDistance
            ? NodeStatus.Success
            : NodeStatus.Failure;
    }

    [SerializeField, Min(0f), Tooltip("プレイヤーを検知する距離")]
    private float _detectDistance = 5f;
}
