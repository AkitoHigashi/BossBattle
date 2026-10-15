using System.Collections.Generic;
using UnityEngine;

public abstract class CompositeNode : BtNode
{
    [SerializeField] protected BtNode[] _childNodes;

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (CompositeNode)base.Clone(clones);

        if (_childNodes == null)
        {
            return clone;
        }

        // 配列の器はInstantiateで作り直されるが、中身は元アセットを指しているので差し替える
        clone._childNodes = new BtNode[_childNodes.Length];

        for (var i = 0; i < _childNodes.Length; i++)
        {
            clone._childNodes[i] = _childNodes[i] != null ? _childNodes[i].Clone(clones) : null;
        }

        return clone;
    }
}
