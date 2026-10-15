using UnityEngine;
[CreateAssetMenu(fileName = "SelectorNode", menuName = "Behavior Tree/Mock/Nodes/Composite Nodes/Selector Node")]
public sealed class SelectorNode : CompositeNode
{
    protected override NodeStatus OnUpdate()
    {
        return NodeStatus.Success;
    }
}
