/// <summary>
/// 毎フレーム先頭の子から評価し直すSelector
/// 優先度の高い子が成功・実行中になったら、それより後ろで実行中だった子は評価されなくなり、
/// BehaviorTree.Update で中断される
/// </summary>
public sealed class ReactiveSelectorNode : CompositeNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "毎フレーム、子ノードを左から順に試し、最初にFailure以外を返した子の結果を返す。\n優先度の高い子の条件が満たされたら、後ろで実行中の子は中断される。全て失敗したらFailure。";

    protected override NodeStatus OnUpdate()
    {
        if (_childNodes == null)
        {
            return NodeStatus.Failure;
        }

        foreach (var child in _childNodes)
        {
            var status = child != null ? child.Evaluate() : NodeStatus.Failure;
            if (status != NodeStatus.Failure)
            {
                return status;
            }
        }

        return NodeStatus.Failure;// 全ての子が失敗した
    }
}
