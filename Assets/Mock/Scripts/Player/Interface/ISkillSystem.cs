using UnityEngine;

/// <summary>
/// スキルの発動要求を受け付ける窓口。
/// </summary>
public interface ISkillSystem
{
    /// <summary>
    /// スキルの発動を試みる。クールダウン中・別スキル実行中などで発動できなければ false を返す。
    /// </summary>
    /// <param name="skillDefinition">発動するスキル。</param>
    /// <param name="direction">発動時の方向（通常はマウス方向）。</param>
    bool TryUseSkill(SkillDefinition skillDefinition,Vector3 direction);
}
