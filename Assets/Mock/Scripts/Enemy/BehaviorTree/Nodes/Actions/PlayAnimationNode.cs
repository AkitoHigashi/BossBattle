using UnityEngine;

public class PlayAnimationNode : ActionNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "指定したステートを再生する。Wait For Completion がオンなら、1周再生するかステートを抜けるまで待つ。";
    private const int BaseLayer = 0;
    private const float StateEntryTimeout = 1f;

    [SerializeField, Tooltip("再生するアニメーションの名前")]
    private string _animationName;
    [SerializeField, Tooltip("オンにするとアニメーションが1周再生されるまで待機する")]
    private bool _waitForCompletion;

    private Animator _animator;
    private int _stateHash;

    protected override void OnStart()
    {
        _animator = null;

        var animator = _blackBoard?.Animator;
        if (animator == null || !animator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(_animationName))
        {
            Debug.LogWarning(
                $"[{nameof(PlayAnimationNode)}] Animatorが無効か、アニメーション名が空です。");
            return;
        }

        _stateHash = Animator.StringToHash(_animationName);
        if (!animator.HasState(BaseLayer, _stateHash))
        {
            Debug.LogWarning(
                $"[{nameof(PlayAnimationNode)}] ステート[{_animationName}]が存在しません。");
            return;
        }

        animator.Play(_stateHash, BaseLayer, 0f);

        if (_waitForCompletion)
        {
            animator.Update(0f);

            var stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayer);
            if (!IsRequestedState(stateInfo))
            {
                Debug.LogWarning(
                    $"[{nameof(PlayAnimationNode)}] ステート[{_animationName}]に入れませんでした。");
                return;
            }
        }

        _animator = animator;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
            return NodeStatus.Failure;

        if (!_waitForCompletion)
            return NodeStatus.Success;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (!IsRequestedState(stateInfo) || stateInfo.normalizedTime >= 1f)
            return NodeStatus.Success;

        return NodeStatus.Running;
    }

    private bool IsRequestedState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.fullPathHash == _stateHash ||
               stateInfo.shortNameHash == _stateHash;
    }
}
