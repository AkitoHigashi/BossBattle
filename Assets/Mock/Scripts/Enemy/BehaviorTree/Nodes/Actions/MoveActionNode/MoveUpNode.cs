using UnityEngine;

/// <summary>
/// 現在の高さから上昇する
/// </summary>
[CreateAssetMenu(
    fileName = "MoveUpNode",
    menuName = "Behavior Tree/Mock/Nodes/Action Nodes/MoveUpNode")]
public sealed class MoveUpNode : MoveVerticalNode
{
    protected override float Direction => 1f;
}
