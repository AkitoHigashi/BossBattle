using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RootNode", menuName = "Behavior Tree/Mock/Nodes/RootNode")]
public sealed class RootNode : BtNode
{
    [SerializeField] private BtNode _childNode;

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (RootNode)base.Clone(clones);
        clone._childNode = _childNode != null ? _childNode.Clone(clones) : null;
        return clone;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null)
        {
            return NodeStatus.Failure;
        }

        return _childNode.Evaluate();// 子の結果をそのまま上へ流す
    }
}
