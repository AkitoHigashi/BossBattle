using UnityEngine;
using UnityEngine.AI;

/// <summary>高度をbaseOffsetで保ったまま、プレイヤーのXZ位置へ追従して停止する。</summary>
public sealed class MoveToTargetPositionNode : ActionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "高さを保ったまま、NavMeshの経路に沿ってターゲットのXZ位置まで移動する。\nArrival Distance以内に着いたらSuccess、Timeoutを過ぎたらFailure。";

    [SerializeField, Min(0.01f), Tooltip("移動中のNavMeshAgentの速さ。終わったら元の速さに戻す")] private float _moveSpeed = 12f;
    [SerializeField, Min(0.01f), Tooltip("ターゲットとの水平距離がこれ以内になったら到着")] private float _arrivalDistance = 0.3f;
    [SerializeField, Min(0.01f), Tooltip("ターゲットや足元の位置からNavMeshを探す半径")] private float _sampleRadius = 2f;
    [SerializeField, Min(0.01f), Tooltip("この秒数を過ぎても着かなければFailure")] private float _timeout = 10f;
    [SerializeField, Min(0.01f), Tooltip("経路を引き直す間隔（秒）。動くターゲットへ追従するため")] private float _repathInterval = 0.1f;

    private float _startTime;
    private float _nextRepathTime;
    private float _originalSpeed;
    private float _originalStoppingDistance;
    private NavMeshPath _path;

    protected override void OnStart()
    {
        _startTime = Time.time;
        _nextRepathTime = Time.time;
        _path ??= new NavMeshPath();

        // 黒板のAgentは差し替わらないので、OnEndで同じAgentへ戻せる
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null)
            return;

        _originalSpeed = agent.speed;
        _originalStoppingDistance = agent.stoppingDistance;
        agent.speed = Mathf.Max(0.01f, _moveSpeed);
        agent.stoppingDistance = Mathf.Max(0.01f, _arrivalDistance) * 0.5f;
    }

    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        var target = _blackBoard?.Target;
        if (agent == null || target == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        Vector3 offset = target.transform.position - agent.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= Mathf.Pow(Mathf.Max(0.01f, _arrivalDistance), 2f))
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 ground = agent.transform.position - Vector3.up * agent.baseOffset;
            if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, _sampleRadius), filter))
                return NodeStatus.Failure;

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

    protected override void OnEnd()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null)
            return;

        agent.speed = _originalSpeed;
        agent.stoppingDistance = _originalStoppingDistance;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }
}
