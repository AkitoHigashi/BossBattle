using UnityEngine;

/// <summary>渦の予兆を一定時間表示してから次のノードへ進む。</summary>
public sealed class ShowSwirlNode : ActionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText = "足元に指定方向の渦を表示する。Durationが経過すると渦を消してSuccess。";

    [SerializeField] private SwirlWarningView _prefab;
    [SerializeField,Tooltip("渦の回転方向。trueなら時計回り、falseなら反時計回り")]
    private bool _clockwise = true;
    [SerializeField, Min(0.01f),Tooltip("表示時間")]
    private float _duration = 1.5f;
    [SerializeField, Min(0.01f),Tooltip("地面探索半径")]
    private float _groundSearchRadius = 2f;
    [SerializeField, Min(0f),Tooltip("表面オフセット")]
    private float _surfaceOffset = 0.07f;

    private SwirlWarningView _view;
    private float _startTime;
    private bool _isShown;

    protected override void OnStart()
    {
        if (_prefab == null || _blackBoard == null)
            return;

        if (!AttackPose.TryCreateGroundAttackPose(
            _blackBoard.Transform,
            _blackBoard.NavMeshAgent,
                _groundSearchRadius,
                0f,
                out AttackPose pose))
            return;

        _startTime = Time.time;


        _view = Instantiate(
            _prefab,
            pose.Origin + Vector3.up * _surfaceOffset,
            Quaternion.identity);

        _isShown = _view.TryShow(_clockwise);
    }

    protected override NodeStatus OnUpdate()
    {
        if (!_isShown || _view == null)
            return NodeStatus.Failure;

        float elapsed = Time.time - _startTime;
        if (elapsed >= Mathf.Max(0.01f, _duration))
            return NodeStatus.Success;

        _view.SetElapsedTime(elapsed);
        return NodeStatus.Running;
    }

    protected override void OnEnd()
    {
        _isShown = false;
        if (_view == null)
            return;

        // Destroyが処理される前に次の攻撃ノードが開始する。
        _view.gameObject.SetActive(false);
        Destroy(_view.gameObject);
        _view = null;
    }
}
