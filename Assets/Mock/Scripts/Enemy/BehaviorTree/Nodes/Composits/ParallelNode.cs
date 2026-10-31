using System.Collections.Generic;
using UnityEngine;

/// <summary>全ての子を並列実行するノード</summary>
[CreateAssetMenu(fileName = "ParallelNode", menuName = "Behavior Tree/Mock/Nodes/Composite Nodes/Parallel Node")]
public class ParallelNode : CompositeNode
{
    private NodeStatus[] _results;
    private bool _isRunning;

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (ParallelNode)base.Clone(clones);
        clone._results = null;
        clone._isRunning = false;
        return clone;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNodes == null || _childNodes.Length == 0)
            return NodeStatus.Failure;

        if (!_isRunning)
        {
            if (_results == null || _results.Length != _childNodes.Length)
                _results = new NodeStatus[_childNodes.Length];

            for (int i = 0; i < _results.Length; i++)
                _results[i] = NodeStatus.Running;

            _isRunning = true;
        }

        bool hasRunning = false;
        bool hasFailure = false;
        for (int i = 0; i < _childNodes.Length; i++)
        {
            // 終了した子は、他の子が動いている間に再実行しない。
            if (_results[i] == NodeStatus.Running)
                _results[i] = _childNodes[i] != null ? _childNodes[i].Evaluate() : NodeStatus.Failure;

            hasRunning |= _results[i] == NodeStatus.Running;
            hasFailure |= _results[i] == NodeStatus.Failure;
        }

        if (hasRunning)
            return NodeStatus.Running;

        _isRunning = false;
        return hasFailure ? NodeStatus.Failure : NodeStatus.Success;
    }

    protected override void OnAbort() => _isRunning = false;
}
