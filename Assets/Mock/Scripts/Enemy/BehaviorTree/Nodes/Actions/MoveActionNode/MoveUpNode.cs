/// <summary>
/// 現在の高さから上昇する
/// </summary>
public sealed class MoveUpNode : MoveVerticalNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "今の高さからMove Distanceだけ上昇する（NavMeshAgentのbaseOffsetを上げる）。\n上がりきったらSuccess。上昇中はその場で止まる。";

    protected override float Direction => 1f;
}
