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
    /// 現在実行中のスキルに割り込めるか
    /// </summary>
    public bool CanInterruptOthers => _canInterruptOthers;
    /// <summary>
    /// 別のスキルによって中断されてもよいか
    /// </summary>
    public bool CanBeInterrupted => _canBeInterrupted;

    /// <summary>
    /// クールダウンの開始タイミング。
    /// </summary>
    public CooldownStartPolicy CooldownPolicy => _cooldownPolicy;

    [SerializeField] private string _skillName;
    [SerializeField, Min(0f)] private float _cooldown;
    [SerializeField] private SkillAction[] _actions;
    [SerializeField,Tooltip("スキルのクールダウンを開始時にするか終わりにするか")] private CooldownStartPolicy _cooldownPolicy;
    [SerializeField,Tooltip("現在実行中のスキルに割り込めるか")] private bool _canInterruptOthers;
    [SerializeField,Tooltip("別のスキルによって中断されてもよいか")] private bool _canBeInterrupted;
}
