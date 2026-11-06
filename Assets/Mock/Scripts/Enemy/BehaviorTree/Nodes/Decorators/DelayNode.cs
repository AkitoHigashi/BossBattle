using UnityEngine;

/// <summary>子の開始を遅らせるノード</summary>
public sealed class DelayNode : DecoratorNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Durationの秒数だけ待ってから子ノードを実行し、その結果を返す。";

    [SerializeField, Min(0f), Tooltip("子ノードを実行するまで待つ秒数")] private float _duration = 1f;

    private bool _childStarted;
    private float _startTime;

    protected override void OnStart()
    {
        _startTime = 0;
        _childStarted = false;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null)
            return NodeStatus.Failure;


        if (_startTime > _duration)
        {
            _childStarted = true;
        }
        _startTime += Time.deltaTime;
        if (_startTime < _duration && !_childStarted)
        {
            return NodeStatus.Running;
        }

        return _childNode.Evaluate();
    }
}
