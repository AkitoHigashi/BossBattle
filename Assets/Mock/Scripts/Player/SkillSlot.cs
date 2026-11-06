/// <summary>
/// プレイヤーが入力で使えるスキル1つ分の情報（どのスキルを・どのキーで使うか）。
/// </summary>
/// <remarks>
/// スキルとキーの対応は PlayerController が持っている。UI などがそれを別に設定すると、
/// スキルやキーを差し替えたときに片方だけ変え忘れて表示がずれるため、PlayerController から受け取る形にしている。
/// </remarks>
public readonly struct SkillSlot
{
    /// <summary>このキーで使うスキル。</summary>
    public SkillDefinition Skill { get; }
    /// <summary>入力キーの表示名（例：Shift+Q）。InputAction の Binding から作る。</summary>
    public string KeyLabel { get; }
    /// <summary>Shift を押しながら使うスキルか。UI で Shift 押下中の表示を切り替えるのに使う。</summary>
    public bool UsesShift { get; }

    public SkillSlot(SkillDefinition skill, string keyLabel, bool usesShift)
    {
        Skill = skill;
        KeyLabel = keyLabel ?? string.Empty;
        UsesShift = usesShift;
    }
}
