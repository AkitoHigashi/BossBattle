using UnityEngine;
public class NearByPlayerNode : ConditionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "ターゲットが黒板のAttack Range以内にいれば子ノードを実行し、その結果を返す。範囲外ならFailure。";

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null || _blackBoard?.Target == null || _blackBoard.Transform == null)
        {
            return NodeStatus.Failure;
        }

        if(Vector3.Distance(_blackBoard.Target.transform.position, _blackBoard.Transform.position) < _blackBoard.AttackRange)
        {
            return _childNode.Evaluate();
        }

        return NodeStatus.Failure;
    }
}
