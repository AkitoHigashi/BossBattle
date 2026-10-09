using UnityEngine;
public readonly struct AttackContext
{
    public AttackContext(Character attacker, AttackData attackData, Vector3 attackPosition, Vector3 attackDirection)
    {
        Attacker = attacker;
        AttackData = attackData;
        AttackPosition = attackPosition;
        AttackDirection = attackDirection.sqrMagnitude > 0f ? attackDirection.normalized : Vector3.forward;
    }

    public readonly Character Attacker;
    public readonly AttackData AttackData;
    public readonly Vector3 AttackPosition;
    public readonly Vector3 AttackDirection;
}
