using UnityEngine;

public enum CooldownStartPolicy
{
    OnStart,
    OnEnd
}

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

    /// <summary>
    /// クールダウンの開始タイミング。
    /// </summary>
    public CooldownStartPolicy CooldownPolicy => _cooldownPolicy;

    [SerializeField] private string _skillName;
    [SerializeField, Min(0f)] private float _cooldown;
    [SerializeField] private SkillAction[] _actions;
    [SerializeField] private CooldownStartPolicy _cooldownPolicy;
}
