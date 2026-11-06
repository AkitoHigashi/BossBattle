using UnityEngine;

/// <summary>子が終わってから一定時間、子を実行させないノード</summary>
public sealed class CooldownNode : DecoratorNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "子ノードを実行し、その結果を返す。\n子がSuccess・Failureで終わってからCooldownの秒数が経つまでは、子を実行せずFailureを返す。\n中断された時はクールダウンに入らない。";

    [SerializeField, Min(0f), Tooltip("子が終わってから、再び子を実行できるまでの秒数")] private float _cooldown = 3f;

    private float _readyTime;

    protected override NodeStatus OnUpdate()
    {
        if (_childNode == null)
            return NodeStatus.Failure;

        if (Time.time < _readyTime)
            return NodeStatus.Failure;

        var status = _childNode.Evaluate();
        if (status != NodeStatus.Running)
            _readyTime = Time.time + Mathf.Max(0f, _cooldown);

        return status;
    }
}
