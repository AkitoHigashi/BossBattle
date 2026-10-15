using GameKit.Dependencies.Utilities.Types;
using UnityEngine;

/// <summary>
/// 攻撃が可能な範囲にターゲットがいるかを判定する条件ノード
/// </summary>
[CreateAssetMenu(
    fileName = "InAttackRangeNode",
    menuName = "Behavior Tree/Mock/Nodes/Condition Nodes/InAttackRangeNode")]
public sealed class InAttackRangeNode : ConditionNode
{
    private bool _isChildRunning;

    protected override NodeStatus OnUpdate()
    {
        if (_childNode is not AreaAttackNode attack)
        {
            _isChildRunning = false;
            return NodeStatus.Failure;
        }

        // 攻撃を開始する前だけ範囲を確認する。
        if (!_isChildRunning)
        {
            if (_blackBoard?.Transform == null ||
                _blackBoard.Target == null)
            {
                return NodeStatus.Failure;
            }

            var owner = _blackBoard.Transform;
            var target = _blackBoard.Target.transform;

            // 攻撃が持つ範囲を受け取って判定する。
            SectorRange range = attack.Range;

            if (!range.Contains(
                    owner.position,
                    owner.forward,
                    target.position))
            {
                return NodeStatus.Failure;
            }
        }

        // 開始後は範囲を再確認せず、硬直終了まで継続。
        var status = attack.Evaluate();

        _isChildRunning = status == NodeStatus.Running;

        return status;
    }
}