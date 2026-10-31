using UnityEngine;
using UnityEngine.AI;

/// <summary>現在のY座標を保ち、プレイヤーのXZ座標にワープする。</summary>
[CreateAssetMenu(fileName = "WarpToTargetXZNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/WarpToTargetXZNode")]
public sealed class WarpToTargetXZNode : ActionNode
{
    [SerializeField, Min(0.01f)] private float _sampleRadius = 2f;

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        var target = _blackBoard?.Target;
        if (agent == null || target == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        Vector3 targetPosition = target.transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit hit,
                Mathf.Max(0.01f, _sampleRadius), filter))
            return NodeStatus.Failure;

        // NavMeshの外だった場合、近くの別のXZ位置へ勝手にずらさない。
        Vector3 difference = hit.position - targetPosition;
        difference.y = 0f;
        if (difference.sqrMagnitude > 0.01f * 0.01f)
            return NodeStatus.Failure;

        float previousOffset = agent.baseOffset;
        Vector3 destination = targetPosition;
        destination.y = agent.transform.position.y;
        agent.baseOffset = destination.y - hit.position.y;
        if (!agent.Warp(destination))
        {
            agent.baseOffset = previousOffset;
            return NodeStatus.Failure;
        }

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        return NodeStatus.Success;
    }
}
