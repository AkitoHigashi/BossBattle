public sealed class SelectorNode : CompositeNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードを左から順に試し、最初に成功した子の結果を返す。\n全ての子が失敗したらFailure。";

    protected override NodeStatus OnUpdate()
    {
        while (_index < _childNodes.Length)
        {
            var child = _childNodes[_index];
            var status = child != null ? child.Evaluate() : NodeStatus.Failure;

            if (status == NodeStatus.Running)
            {
                return NodeStatus.Running;
            }

            if (status == NodeStatus.Success)
            {
                _index = 0;
                return NodeStatus.Success;
            }
            _index++;
        }
        _index = 0;
        return NodeStatus.Failure;// 全ての子が失敗した
    }

    protected override void OnAbort() => _index = 0;

    private int _index;
}
