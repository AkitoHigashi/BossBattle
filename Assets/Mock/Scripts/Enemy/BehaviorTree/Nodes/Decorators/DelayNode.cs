using System.Collections.Generic;
using UnityEngine;

/// <summary>子の開始を遅らせるノード</summary>
[CreateAssetMenu(fileName = "DelayNode", menuName = "Behavior Tree/Mock/Nodes/Decorator Nodes/Delay Node")]
public sealed class DelayNode : DecoratorNode
{
    [SerializeField, Min(0f)] private float _duration = 1f;

    private bool _isRunning;
    private bool _childStarted;
    private float _startTime;

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (DelayNode)base.Clone(clones);
        clone.ResetDelay();
        return clone;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null)
            return NodeStatus.Failure;

        if (!_isRunning)
        {
            _startTime = Time.time;
            _isRunning = true;
        }

        if (!_childStarted)
        {
            if (Time.time - _startTime < Mathf.Max(0f, _duration))
                return NodeStatus.Running;

            _childStarted = true;
        }

        return _childNode.Evaluate();
    }

    protected override void OnSuccess() => ResetDelay();
    protected override void OnFailure() => ResetDelay();

    private void ResetDelay()
    {
        _isRunning = false;
        _childStarted = false;
        _startTime = 0f;
    }

    protected override void OnAbort() => ResetDelay();
}
