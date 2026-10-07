using System.Collections.Generic;
using UnityEngine;

public abstract class DecoratorNode : BtNode
{
    [SerializeField] protected BtNode _childNode;

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (DecoratorNode)base.Clone(clones);
        clone._childNode = _childNode != null ? _childNode.Clone(clones) : null;
        return clone;
    }
}
