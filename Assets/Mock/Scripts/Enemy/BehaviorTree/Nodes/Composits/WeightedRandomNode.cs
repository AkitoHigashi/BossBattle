using UnityEngine;

public sealed class WeightedRandomNode : CompositeNode
{
    public override string Description => DescriptionText;

    private const string DescriptionText =
        "子ノードの中から重み付きでランダムに1つを選んで評価する。SuccessならSuccess、FailureならFailureを返す。";

    protected override void OnStart()
    {
        // 初期化
        _index = -1;
        
        // 子ノードの重みを合計して、その中からランダムに1つを選ぶ
        if (_childNodes != null && _childNodes.Length > 0)
        {
            int totalWeight = 0;
            foreach (var child in _childNodes)
            {
                totalWeight += child.Weight;
            }

            int randomValue = Random.Range(0, totalWeight);
            int cumulativeWeight = 0;
            for (int i = 0; i < _childNodes.Length; i++)
            {
                cumulativeWeight += _childNodes[i].Weight;
                if (randomValue < cumulativeWeight)
                {
                    _index = i;
                    break;
                }
            }
        }
    }
    protected override NodeStatus OnUpdate()
    {
        if (_childNodes == null || _index < 0 || _index >= _childNodes.Length)
        {
            return NodeStatus.Failure;
        }

        return _childNodes[_index].Evaluate();
    }

    private int _index;
}
