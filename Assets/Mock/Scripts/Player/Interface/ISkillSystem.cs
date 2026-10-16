using UnityEngine;

public interface ISkillSystem
{
    bool TryUseSkill(SkillDefinition skillDefinition,Vector3 direction);
}
