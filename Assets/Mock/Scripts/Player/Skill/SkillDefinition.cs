using UnityEngine;

/// <summary>
/// スキルごとの設定データ。
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "BossBattle/Skill")]
public class SkillDefinition : ScriptableObject
{
    /// <summary>
    /// スキルの名前を取得する。
    /// </summary>
    public string SkillName => _skillName;
    /// <summary>
    /// スキルのクールダウン時間。
    /// </summary>
    public float Cooldown => _cooldown;
    /// <summary>
    /// スキルの動作を取得する。
    /// </summary>
    public SkillAction[] Actions => _actions;

    [SerializeField] private string _skillName;
    [SerializeField, Min(0f)] private float _cooldown;
    [SerializeField] private SkillAction[] _actions;
}
