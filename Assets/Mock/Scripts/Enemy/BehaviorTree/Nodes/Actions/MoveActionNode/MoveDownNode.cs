using UnityEngine;

/// <summary>
/// 下に下がるよ
/// </summary>
[CreateAssetMenu(
    fileName = "MoveDownNode",
    menuName = "Behavior Tree/Mock/Nodes/Action Nodes/MoveDownNode")]
public sealed class MoveDownNode : MoveVerticalNode
{
    protected override float Direction => -1f;
}