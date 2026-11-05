public sealed class SequenceNode : CompositeNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードを左から順に実行していくノード。\n1つでも失敗したらFailure、全て成功したらSuccess。";

    // 次に開始された時は最初の子からやり直す
    protected override void OnStart() => _index = 0;

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

            if (status == NodeStatus.Failure)
            {
                return NodeStatus.Failure;
            }
            _index++;
        }
        return NodeStatus.Success;
    }

    private int _index;
}
