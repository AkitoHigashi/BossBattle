using System.Collections.Generic;

public readonly struct AttackData
{
    public AttackData(IReadOnlyList<IHitEffect> hitEffects, float damage)
    {
        HitEffects = hitEffects;
        Damage = damage;
    }
    
    public IReadOnlyList<IHitEffect> HitEffects { get; }
    public float Damage { get; }

}
