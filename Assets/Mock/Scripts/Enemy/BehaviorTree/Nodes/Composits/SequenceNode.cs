using UnityEngine;

[CreateAssetMenu(fileName = "SequenceNode", menuName = "Behavior Tree/Mock/Nodes/Composite Nodes/Sequence Node")]
public sealed class SequenceNode : CompositeNode
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

            if (status == NodeStatus.Failure)
            {
                _index = 0;// 次は最初の子からやり直す
                return NodeStatus.Failure;
            }
            _index++;
        }
        _index = 0;
        return NodeStatus.Success;
    }

    protected override void OnAbort() => _index = 0;

    private int _index;
}
