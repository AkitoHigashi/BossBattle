using UnityEngine;
using UnityEngine.AI;

public abstract class MoveVerticalNode : ActionNode
{
    [SerializeField, Min(0.01f)]
    private float _moveDistance = 3f;

    [SerializeField, Min(0.01f)]
    private float _moveSpeed = 2f;

    private float _targetOffset;

    // 上は1、下は-1
    protected abstract float Direction { get; }

    protected override void OnStart()
    {
        NavMeshAgent agent = _blackBoard?.NavMeshAgent;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        _targetOffset =
            agent.baseOffset + Direction * _moveDistance;
    }

    protected override NodeStatus OnUpdate()
    {
        NavMeshAgent agent = _blackBoard?.NavMeshAgent;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        agent.baseOffset = Mathf.MoveTowards(
            agent.baseOffset,
            _targetOffset,
            Mathf.Max(0.01f, _moveSpeed) * Time.deltaTime);

        if (!Mathf.Approximately(
                agent.baseOffset,
                _targetOffset))
        {
            return NodeStatus.Running;
        }

        return NodeStatus.Success;
    }
}
