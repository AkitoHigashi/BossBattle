using UnityEngine;
using UnityEngine.AI;

/// <summary>地面に出す予測表示の時間を管理する</summary>
[CreateAssetMenu(fileName = "AttackPredictionNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackPredictionNode")]
public sealed class AttackPredictionNode : ActionNode
{
    [SerializeField] private SectorRangeSettings _range;
    [SerializeField, Min(0f)] private float _duration = 2f;
    [SerializeField, Min(0.01f)] private float _sampleRadius = 2f;

    private bool _isRunning;
    private float _startTime;

    protected override NodeStatus OnUpdate()
    {
        var view = _blackBoard?.PredictionView;
        if (_range == null || view == null || !view.isActiveAndEnabled || _blackBoard.Transform == null)
            return NodeStatus.Failure;

        if (!_isRunning)
        {
            var attack = _blackBoard.Attack;
            attack.HasPrediction = false;
            attack.DamageApplied = false;
            if (!TryGetGroundOrigin(out Vector3 origin))
                return NodeStatus.Failure;

            var pose = new AttackPose(origin, _blackBoard.Transform.forward);
            var range = _range.Range;
            if (!view.Show(range, pose))
                return NodeStatus.Failure;

            // 位置・形状を一度だけ確定し、表示と着地時の判定で同じ値を使う。
            attack.Pose = pose;
            attack.Range = range;
            attack.HasPrediction = true;

            _startTime = Time.time;
            _isRunning = true;
        }

        float progress = _duration <= 0f ? 1f : Mathf.Clamp01((Time.time - _startTime) / _duration);
        view.SetProgress(progress);
        if (progress < 1f)
            return NodeStatus.Running;

        _isRunning = false;

        // 表示と確定データは残し、後続の攻撃ノードが使用する。
        return NodeStatus.Success;
    }

    private bool TryGetGroundOrigin(out Vector3 origin)
    {
        origin = _blackBoard.Transform.position;
        var agent = _blackBoard.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 ground = origin - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, _sampleRadius), filter))
            return false;

        origin.y = hit.position.y;
        return true;
    }

    protected override void OnFailure()
    {
        _isRunning = false;
        if (_blackBoard != null)
        {
            _blackBoard.Attack.HasPrediction = false;
            _blackBoard.Attack.DamageApplied = false;
        }
        _blackBoard?.PredictionView?.Hide();
    }

    // 予兆の途中で中断されたら、表示と確定データを片付ける
    protected override void OnAbort() => OnFailure();
}
