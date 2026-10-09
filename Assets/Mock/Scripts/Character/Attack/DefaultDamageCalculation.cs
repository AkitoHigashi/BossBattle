using UnityEngine;

[CreateAssetMenu(fileName = "DefaultDamageCalculation", menuName = "BossBattle/Damage Calculation/Default")]
public class DefaultDamageCalculation : DamageCalculation
{
    public override int CalculateDamage(AttackContext context, HitInfo hitInfo)
    {
        int attackPower = context.Attacker != null ? context.Attacker.Status.AttackPower : 0;
        int defensePower = hitInfo.Target != null ? hitInfo.Target.Status.DefensePower : 0;
        return Mathf.Max(0, context.AttackData.BaseDamage + attackPower - defensePower);
    }
}
