using UnityEngine;

/// <summary>子の開始を遅らせるノード</summary>
public sealed class DelayNode : DecoratorNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Durationの秒数だけ待ってから子ノードを実行し、その結果を返す。";

    [SerializeField, Min(0f)] private float _duration = 1f;

    private bool _childStarted;
    private float _startTime;

    protected override void OnStart()
    {
        _startTime = Time.time;
        _childStarted = false;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null)
            return NodeStatus.Failure;

        if (!_childStarted)
        {
            if (Time.time - _startTime < Mathf.Max(0f, _duration))
                return NodeStatus.Running;

            _childStarted = true;
        }

        return _childNode.Evaluate();
    }
}
