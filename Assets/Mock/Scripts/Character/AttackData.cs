using System.Collections.Generic;

public readonly struct AttackData
{
    public AttackData(IReadOnlyList<IHitEffect> hitEffects, float damage)
    {
        HitEffect = hitEffects;
        Damage = damage;
    }
    
    public IReadOnlyList<IHitEffect> HitEffect { get; }
    public float Damage { get; }

}
