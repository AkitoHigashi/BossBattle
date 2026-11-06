using UnityEngine;

public class PlayAnimationNode : ActionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "指定したステートを再生する。Wait For Completion がオンなら、1周再生するかステートを抜けるまで待つ。";
    private const int BaseLayer = 0;
    private const float StateEntryTimeout = 1f;

    [SerializeField] private string _animationName;
    [SerializeField] private bool _waitForCompletion;

    private Animator _animator;
    private int _stateHash;
    private int _playFrame;
    private double _playFixedTime;
    private float _stateEntryDeadline;
    private bool _enteredState;

    protected override void OnStart()
    {
        _animator = null;
        _enteredState = false;

        var animator = _blackBoard?.Animator;
        if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrWhiteSpace(_animationName))
            return;

        _stateHash = Animator.StringToHash(_animationName);
        if (!animator.HasState(BaseLayer, _stateHash))
            return;

        _animator = animator;
        _playFrame = Time.frameCount;
        _playFixedTime = Time.fixedTimeAsDouble;
        _stateEntryDeadline = Time.time + StateEntryTimeout;
        animator.Play(_stateHash, BaseLayer, 0f);
    }

    protected override NodeStatus OnUpdate()
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
            return NodeStatus.Failure;

        if (!_waitForCompletion)
            return NodeStatus.Success;

        // Animatorが更新されてから進行度を見る。
        if (Time.frameCount == _playFrame ||
            (_animator.updateMode == AnimatorUpdateMode.Fixed && Time.fixedTimeAsDouble == _playFixedTime))
            return NodeStatus.Running;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (stateInfo.fullPathHash == _stateHash || stateInfo.shortNameHash == _stateHash)
        {
            _enteredState = true;
            return stateInfo.normalizedTime >= 1f ? NodeStatus.Success : NodeStatus.Running;
        }

        // Controller が再生後に遷移した場合も待機を終える。
        if (_enteredState)
            return NodeStatus.Success;

        return Time.time >= _stateEntryDeadline ? NodeStatus.Failure : NodeStatus.Running;
    }
}
