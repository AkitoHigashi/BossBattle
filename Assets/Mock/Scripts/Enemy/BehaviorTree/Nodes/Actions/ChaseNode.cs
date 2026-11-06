using UnityEngine;

/// <summary>
/// ターゲットを追跡するアクションノード
/// </summary>
public sealed class ChaseNode : ActionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "NavMeshでターゲットを追いかける。Arrival Distance以内まで近づいたら止まってSuccess。";

    [SerializeField, Min(0.01f), Tooltip("ターゲットからこの距離以内まで近づいたら完了します。")]
    private float _arrivalDistance = 1.5f;

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        if (_blackBoard.Target == null)
            return NodeStatus.Failure;

        Vector3 targetPosition = _blackBoard.Target.transform.position;

        // 指定距離に到達したら、このノードの処理を完了する。浮いていても届くよう高さは除く。
        Vector3 offset = targetPosition - _blackBoard.Transform.position;
        offset.y = 0f;
        float arrivalDistance = Mathf.Max(0.01f, _arrivalDistance);
        if (offset.sqrMagnitude < arrivalDistance * arrivalDistance)
            return NodeStatus.Success;

        // 親から評価されている間だけ近づく
        agent.isStopped = false;
        return agent.SetDestination(targetPosition) ? NodeStatus.Running : NodeStatus.Failure;
    }

    // 到着・失敗・中断のどれでも、最後の目的地へ歩き続けないように止める
    protected override void OnEnd()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }
}
