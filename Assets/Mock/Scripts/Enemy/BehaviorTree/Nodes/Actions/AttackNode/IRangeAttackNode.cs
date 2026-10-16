using UnityEngine;

/// <summary>
/// 形状に依存せず、攻撃ノードへ範囲判定を依頼するためのインターフェース。
/// 範囲の設定は実装する攻撃ノードが所有する。
/// </summary>
public interface IRangeAttackNode
{
    bool IsInAttackRange(
        Vector3 origin,
        Vector3 forward,
        Vector3 targetPosition);
}
