using UnityEngine;
[CreateAssetMenu(fileName = "SelectorNode", menuName = "Behavior Tree/Mock/Nodes/Composite Nodes/Selector Node")]
public sealed class SelectorNode : CompositeNode
{
    protected override NodeStatus OnUpdate()
    {
        while (_index < _childNodes.Length)
        {
            var child = _childNodes[_index];
            var status = child != null ? child.Evaluate() : NodeStatus.Failure;

            if (status == NodeStatus.Running)
            {
                return NodeStatus.Running;
            }

            if (status == NodeStatus.Success)
            {
                _index = 0;
                return NodeStatus.Success;
            }
            _index++;
        }
        _index = 0;
        return NodeStatus.Failure;// 全ての子が失敗した
    }

    protected override void OnAbort() => _index = 0;

    private int _index;
}
