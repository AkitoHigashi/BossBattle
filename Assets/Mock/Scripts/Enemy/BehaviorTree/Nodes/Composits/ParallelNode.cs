/// <summary>全ての子を並列実行するノード</summary>
public class ParallelNode : CompositeNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "全ての子ノードを同時に実行する。\n全ての子が終わるまでRunning。1つでも失敗していればFailure、全て成功ならSuccess。";

    private NodeStatus[] _results;

    protected override void OnStart()
    {
        if (_childNodes == null)
            return;

        if (_results == null || _results.Length != _childNodes.Length)
            _results = new NodeStatus[_childNodes.Length];

        for (int i = 0; i < _results.Length; i++)
            _results[i] = NodeStatus.Running;
    }

    protected override NodeStatus OnUpdate()
    {
        if (_childNodes == null || _childNodes.Length == 0)
            return NodeStatus.Failure;

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

        return hasFailure ? NodeStatus.Failure : NodeStatus.Success;
    }
}
