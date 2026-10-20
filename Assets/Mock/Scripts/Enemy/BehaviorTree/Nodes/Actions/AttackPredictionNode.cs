using UnityEngine;

/// <summary>予測表示の時間を管理する。描画はView、命中判定は攻撃ノードが担当する。</summary>
[CreateAssetMenu(fileName = "AttackPredictionNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackPredictionNode")]
public sealed class AttackPredictionNode : ActionNode
{
    [SerializeField] private SectorRangeSettings _range;
    [SerializeField, Min(0f)] private float _duration = 2f;

    private bool _isRunning;
    private float _startTime;

    protected override NodeStatus OnUpdate()
    {
        var view = _blackBoard?.PredictionView;
        if (_range == null || view == null || !view.isActiveAndEnabled ||
            _blackBoard.Transform == null || !_blackBoard.AerialAttack.HasLandingPoint)
            return NodeStatus.Failure;

        if (!_isRunning)
        {
            var owner = _blackBoard.Transform;
            Vector3 origin = owner.position;
            origin.y = _blackBoard.AerialAttack.GroundPosition.y;
            var pose = new AttackPose(origin, owner.forward);

            // 位置・形状を一度だけ確定し、表示と着地時の判定で同じ値を使う。
            var attack = _blackBoard.AerialAttack;
            attack.Pose = pose;
            attack.Range = _range.Range;
            attack.HasPrediction = true;
            attack.DamageApplied = false;
            if (!view.Show(attack.Range, pose))
                return NodeStatus.Failure;

            _startTime = Time.time;
            _isRunning = true;
        }

        float progress = _duration <= 0f ? 1f : Mathf.Clamp01((Time.time - _startTime) / _duration);
        view.SetProgress(progress);
        if (progress < 1f)
            return NodeStatus.Running;

        _isRunning = false;

        return NodeStatus.Success;
    }

    protected override void OnFailure()
    {
        _isRunning = false;
        _blackBoard?.PredictionView?.Hide();
    }
}
