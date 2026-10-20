using UnityEngine;
using UnityEngine.AI;

public abstract class MoveVerticalNode : ActionNode
{
    [SerializeField, Min(0.01f)]
    private float _moveDistance = 3f;

    [SerializeField, Min(0.01f)]
    private float _moveSpeed = 2f;

    private bool _isMoving;
    private float _targetOffset;

    // 上は1、下は-1
    protected abstract float Direction { get; }

    protected override NodeStatus OnUpdate()
    {
        NavMeshAgent agent = _blackBoard?.NavMeshAgent;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        if (!_isMoving)
        {
            if (!TryBeginMovement(agent))
                return NodeStatus.Failure;

            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            _targetOffset =
                agent.baseOffset + Direction * _moveDistance;

            _isMoving = true;
        }

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

        _isMoving = false;
        return NodeStatus.Success;
    }

    protected override void OnFailure()
    {
        _isMoving = false;
    }

    protected virtual bool TryBeginMovement(NavMeshAgent agent) => true;
}
