using System.Collections.Generic;
using UnityEngine;

public sealed class RootNode : BtNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "ツリーの開始地点。子ノードを1つだけ持ち、その結果をそのまま返す。";

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
