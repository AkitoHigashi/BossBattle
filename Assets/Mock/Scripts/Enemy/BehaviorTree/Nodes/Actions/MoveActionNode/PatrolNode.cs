using UnityEngine;
using UnityEngine.AI;

public class PatrolNode : ActionNode
{
    public override string Description => "NavmeshAgentを使って巡回するノード。";

    protected override void OnStart()
    {
        _patrolTimer = 0f;

        _hasDestination = TrySetRandomDestination();
    }
    protected override NodeStatus OnUpdate()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (!_hasDestination || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return NodeStatus.Failure;
        }

        // 経路の計算中はremainingDistanceが正しくないので、計算が終わってから判定する
        if (!agent.pathPending && agent.remainingDistance <= _arrivalDistance)
        {
            return NodeStatus.Success;
        }

        _patrolTimer += Time.deltaTime;
        if (_patrolTimer >= _patrolDuration)
        {
            return NodeStatus.Failure;
        }

        return NodeStatus.Running;
    }
    /// <summary>
    /// 目的地に到着、時間切れ、中止のどれでも止める。
    /// </summary>
    protected override void OnEnd()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            // AgentがNavMesh上にいない場合の処理
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }
    /// <summary>
    /// 足元から_patrolRadius以内のNavMesh上の地点を選び、そこへ移動を始める
    /// </summary>
    /// <returns>移動先を設定できたか</returns>
    private bool TrySetRandomDestination()
    {
        // isOnNavMeshでAgentがSurface上にいるかを判定する。Base Offsetで浮いていても足元で判定される
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        // Transformの位置はBase Offsetの分だけ浮いているので、足元の地面の高さを基準にする
        Vector3 ground = agent.transform.position - Vector3.up * agent.baseOffset;
        Vector2 offset = Random.insideUnitCircle * _patrolRadius;
        Vector3 randomPoint = ground + new Vector3(offset.x, 0f, offset.y);

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(randomPoint, out var hit, SampleRadius, filter))
            return false;

        // 他のノードが止めたままにしていることがあるので、動ける状態に戻してから移動先を渡す
        agent.isStopped = false;
        return agent.SetDestination(hit.position);
    }

    // 地面の傾きや段差で、選んだ点が少しずれても拾えるようにする
    private const float SampleRadius = 2f;

    [SerializeField, Min(0.01f), Tooltip("巡回する場所を決めるための半径")]
    private float _patrolRadius = 5f;
    [SerializeField, Min(0.01f), Tooltip("この時間を過ぎても着かなければFailure")]
    private float _patrolDuration = 10f;
    [SerializeField, Min(0.01f), Tooltip("この距離以内まで近づいたら到着")]
    private float _arrivalDistance = 0.5f;
    private float _patrolTimer;
    private bool _hasDestination;
}
