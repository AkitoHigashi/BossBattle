using UnityEngine;

public sealed class AttackWarningNode : DecoratorNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードが動いている間、足元の地面にWarning Areaの予兆を表示する。Wait For Warning Completionがオンなら、Warning Timeの経過後に予兆を消して子ノードを開始する。";

    [SerializeField, Tooltip("表示する予兆の範囲")] private SectorRangeSettings _warningArea;
    [SerializeField, Min(0.01f), Tooltip("足元の地面を探す半径")] private float _groundSearchRadius = 2f;
    [SerializeField, Tooltip("正面を0度とする予兆の水平回転")] private float _yawOffset;

    [SerializeField, Tooltip("Warning Timeが過ぎるまで待ち、予兆を消してから子ノードを開始する")]
    private bool _waitForWarningCompletion;

    private AttackPredictionView _view;
    private float _startTime;

    protected override void OnStart()
    {
        _startTime = Time.time;
        var view = _blackBoard?.PredictionView;
        if (_childNode == null || _warningArea == null || view == null || !view.isActiveAndEnabled)
            return;

        if (!AttackPose.TryCreateGroundAttackPose(_blackBoard.Transform, _blackBoard.NavMeshAgent,
                _groundSearchRadius, _yawOffset, out AttackPose pose))
            return;

        if (view.Show(_warningArea.Range, pose, this))
            _view = view;
    }

    protected override NodeStatus OnUpdate()
    {
        float duration = _warningArea != null ? _warningArea.WarningTime : 0f;
        float elapsed = Time.time - _startTime;
        if (_view != null)
            _view.SetProgress(duration > 0f ? elapsed / duration : 1f, this);

        if (_waitForWarningCompletion && _childNode != null)
        {
            if (elapsed < duration)
                return NodeStatus.Running;

            HideWarning();
        }

        return _childNode != null ? _childNode.Evaluate() : NodeStatus.Failure;
    }

    protected override void OnEnd() => HideWarning();

    private void HideWarning()
    {
        if (_view != null)
            _view.Hide(this);
        _view = null;
    }
}
