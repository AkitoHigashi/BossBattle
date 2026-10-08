using UnityEngine;

[CreateAssetMenu(fileName = "SequenceNode", menuName = "Behavior Tree/Mock/Nodes/Sequence Node")]
public sealed class SequenceNode : CompositeNode
{
    protected override NodeStatus OnUpdate()
    {
        while (_index < _childNodes.Length)
        {
            var child = _childNodes[_index];
            var status = child.Evaluate();

            if (status == NodeStatus.Running)
            {
                return NodeStatus.Running;
            }

            if (status == NodeStatus.Failure)
            {
                return NodeStatus.Failure;
            }
            _index++;
        }
        _index = 0;
        return NodeStatus.Success;
    }

    private int _index;
}
