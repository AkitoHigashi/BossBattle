using UnityEngine;

/// <summary>
/// ターゲットを追跡するアクションノード
/// </summary>
[CreateAssetMenu(fileName = "ChaseNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/ChaseNode")]
public sealed class ChaseNode : ActionNode
{
    [SerializeField, Min(0.01f), Tooltip("ターゲットからこの距離以内まで近づいたら完了します。")]
    private float _arrivalDistance = 1.5f;

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        if (_blackBoard.Target == null)
        {
            agent.ResetPath();
            agent.isStopped = true;
            return NodeStatus.Failure;
        }

        Vector3 targetPosition = _blackBoard.Target.transform.position;

        // 指定距離に到達したら、このノードの処理を完了する。
        if (Vector3.Distance(_blackBoard.Transform.position, targetPosition) <= Mathf.Max(0.01f, _arrivalDistance))
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return NodeStatus.Success;
        }

        // 親から評価されている間だけ近づく
        agent.isStopped = false;
        return agent.SetDestination(targetPosition) ? NodeStatus.Running : NodeStatus.Failure;
    }
}
