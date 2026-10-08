using System.Collections.Generic;
using UnityEngine;

public abstract class ConditionNode : BtNode
{
    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (ConditionNode)base.Clone(clones);
        clone._childNode = _childNode.Clone(clones);
        return clone;
    }

    [SerializeField] private BtNode _childNode;
}
