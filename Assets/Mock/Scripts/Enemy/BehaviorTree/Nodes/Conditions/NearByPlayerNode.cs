using UnityEngine;
[CreateAssetMenu(fileName = "NearByPlayerNode", menuName = "Behavior Tree/Mock/Nodes/Condition Nodes/NearByPlayerNode")]
public class NearByPlayerNode : ConditionNode
{
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
