/// <summary>
/// 下に下がるよ
/// </summary>
public sealed class MoveDownNode : MoveVerticalNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "今の高さからMove Distanceだけ下降する（NavMeshAgentのbaseOffsetを下げる）。\n下がりきったらSuccess。下降中はその場で止まる。";

    protected override float Direction => -1f;
}