using UnityEngine;
using UnityEngine.AI;

/// <summary>高度をbaseOffsetで保ったまま、プレイヤーのXZ位置へ追従して停止する。</summary>
[CreateAssetMenu(fileName = "MoveToTargetPositionNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/MoveToTargetPositionNode")]
public sealed class MoveToTargetPositionNode : ActionNode
{
    [SerializeField, Min(0.01f)] private float _moveSpeed = 12f;
    [SerializeField, Min(0.01f)] private float _arrivalDistance = 0.3f;
    [SerializeField, Min(0.01f)] private float _sampleRadius = 2f;
    [SerializeField, Min(0.01f)] private float _timeout = 10f;
    [SerializeField, Min(0.01f)] private float _repathInterval = 0.1f;

    private bool _isMoving;
    private float _startTime;
    private float _nextRepathTime;
    private float _originalSpeed;
    private float _originalStoppingDistance;
    private NavMeshPath _path;

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        var target = _blackBoard?.Target;
        if (agent == null || target == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh ||
            !_blackBoard.AerialAttack.HasLandingPoint)
            return NodeStatus.Failure;

        if (!_isMoving)
        {
            _originalSpeed = agent.speed;
            _originalStoppingDistance = agent.stoppingDistance;
            agent.speed = Mathf.Max(0.01f, _moveSpeed);
            agent.stoppingDistance = Mathf.Max(0.01f, _arrivalDistance) * 0.5f;
            _startTime = Time.time;
            _nextRepathTime = Time.time;
            _path = new NavMeshPath();
            _isMoving = true;
        }

        Vector3 offset = target.transform.position - agent.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= Mathf.Pow(Mathf.Max(0.01f, _arrivalDistance), 2f))
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 ground = agent.transform.position - Vector3.up * agent.baseOffset;
            if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, _sampleRadius), filter))
                return NodeStatus.Failure;

            _blackBoard.AerialAttack.GroundPosition = hit.position;
            return NodeStatus.Success;
        }

        if (Time.time - _startTime >= Mathf.Max(0.01f, _timeout))
            return NodeStatus.Failure;

        if (Time.time >= _nextRepathTime)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(target.transform.position, out NavMeshHit hit,
                    Mathf.Max(0.01f, _sampleRadius), filter) ||
                !agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete)
                return NodeStatus.Failure;

            agent.isStopped = false;
            if (!agent.SetPath(_path))
                return NodeStatus.Failure;

            _nextRepathTime = Time.time + Mathf.Max(0.01f, _repathInterval);
        }

        return NodeStatus.Running;
    }

    protected override void OnSuccess() => StopAndRestore();
    protected override void OnFailure() => StopAndRestore();

    private void StopAndRestore()
    {
        if (!_isMoving)
            return;

        var agent = _blackBoard?.NavMeshAgent;
        if (agent != null)
        {
            agent.speed = _originalSpeed;
            agent.stoppingDistance = _originalStoppingDistance;
        }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        _isMoving = false;
        _path = null;
    }
}
