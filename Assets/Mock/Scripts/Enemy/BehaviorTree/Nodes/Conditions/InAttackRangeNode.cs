using UnityEngine;

/// <summary>
/// 攻撃が可能な範囲にターゲットがいるかを判定する条件ノード
/// </summary>
[CreateAssetMenu(
    fileName = "InAttackRangeNode",
    menuName = "Behavior Tree/Mock/Nodes/Condition Nodes/InAttackRangeNode")]
public sealed class InAttackRangeNode : ConditionNode
{
    [SerializeField]
    private SectorRangeSettings _range;

    [SerializeField, Min(0f)]
    private float _telegraphTime = 1f;

    private bool _isTelegraphing;
    private bool _isChildRunning;
    private float _finishTime;

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null || _range == null ||
            _blackBoard?.Transform == null)
        {
            _isTelegraphing = false;
            _isChildRunning = false;
            return NodeStatus.Failure;
        }

        // 命中後は描画せず、子の処理（攻撃後の待機など）を完了まで継続する。
        if (_isChildRunning)
            return EvaluateChild();

        if (_blackBoard.Target == null)
        {
            _isTelegraphing = false;
            return NodeStatus.Failure;
        }

        var owner = _blackBoard.Transform;
        var target = _blackBoard.Target.transform;

        if (!_isTelegraphing)
        {
            if (!_range.Contains(owner.position, owner.forward, target.position))
                return NodeStatus.Failure;

            _finishTime = Time.time + Mathf.Max(0f, _telegraphTime);
            _isTelegraphing = true;
        }

        if (Time.time < _finishTime)
        {
            float progress = _telegraphTime > 0f
                ? Mathf.Clamp01(1f - (_finishTime - Time.time) / _telegraphTime)
                : 1f;

            _range.DrawDebug(owner.position, owner.forward, progress);
            return NodeStatus.Running;
        }

        // 予兆終了時に表示を止め、範囲内なら子を実行する。
        _isTelegraphing = false;
        if (!_range.Contains(
                owner.position,
                owner.forward,
                target.position))
        {
            return NodeStatus.Failure;
        }

        return EvaluateChild();
    }

    private NodeStatus EvaluateChild()
    {
        var status = _childNode.Evaluate();
        _isChildRunning = status == NodeStatus.Running;
        return status;
    }
}
