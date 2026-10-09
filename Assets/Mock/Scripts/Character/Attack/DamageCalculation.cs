using UnityEngine;

public abstract class DamageCalculation : ScriptableObject, IDamageCalculation
{
    public abstract int CalculateDamage(AttackContext context, HitInfo hitInfo);
}
