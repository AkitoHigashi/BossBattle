using UnityEngine;

/// <summary>
/// ターゲットの方向を向くノード
/// </summary>
[CreateAssetMenu(fileName = "FaceTargetNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/FaceTargetNode")]
public class FaceTargetNode : ActionNode
{

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null)
            return NodeStatus.Failure;

        var target = _blackBoard.Target;
        if (target == null)
            return NodeStatus.Failure;

        Vector3 direction =
            target.transform.position - agent.transform.position;

        direction.y = 0f;

        agent.transform.rotation =
            Quaternion.LookRotation(direction, Vector3.up);

        return NodeStatus.Success;
    }
}
