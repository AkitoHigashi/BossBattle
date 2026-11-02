using UnityEngine;

public sealed class RandomSelectorNode : CompositeNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードの中からランダムに1つを選んで評価する。SuccessならSuccess、FailureならFailureを返す。";

    protected override void OnStart()
    {
          _index = _childNodes != null && _childNodes.Length > 0 ? Random.Range(0, _childNodes.Length) : -1;
    }
    protected override NodeStatus OnUpdate()
    {
        if(_childNodes == null || _index < 0 || _index >= _childNodes.Length)
        {
            return NodeStatus.Failure;
        }

       return _childNodes[_index].Evaluate();
    }

    private int _index;
}
