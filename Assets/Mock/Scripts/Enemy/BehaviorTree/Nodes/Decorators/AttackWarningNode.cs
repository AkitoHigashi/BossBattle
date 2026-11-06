using UnityEngine;

public sealed class AttackWarningNode : DecoratorNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードが動いている間、足元の地面にWarning Areaの予兆を表示する。Warning Timeは塗りが広がる時間。";

    [SerializeField, Tooltip("表示する予兆の範囲")] private SectorRangeSettings _warningArea;
    [SerializeField, Min(0.01f), Tooltip("足元の地面を探す半径")] private float _groundSearchRadius = 2f;
    [SerializeField, Tooltip("正面を0度とする予兆の水平回転")] private float _yawOffset;

    private AttackPredictionView _view;
    private float _startTime;

    protected override void OnStart()
    {
        var view = _blackBoard?.PredictionView;
        if (_childNode == null || _warningArea == null || view == null || !view.isActiveAndEnabled)
            return;

        if (!AttackPose.TryAtGround(_blackBoard.Transform, _blackBoard.NavMeshAgent,
                _groundSearchRadius, _yawOffset, out AttackPose pose))
            return;

        if (view.Show(_warningArea.Range, pose, this))
        {
            _view = view;
            _startTime = Time.time;
        }
    }

    protected override NodeStatus OnUpdate()
    {
        if (_view != null)
        {
            float duration = _warningArea.WarningTime;
            _view.SetProgress(duration > 0f ? (Time.time - _startTime) / duration : 1f, this);
        }

        return _childNode != null ? _childNode.Evaluate() : NodeStatus.Failure;
    }

    protected override void OnEnd()
    {
        if (_view != null)
            _view.Hide(this);
        _view = null;
    }
}
