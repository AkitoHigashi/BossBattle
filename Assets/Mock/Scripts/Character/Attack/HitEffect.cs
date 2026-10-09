using UnityEngine;

public abstract class HitEffect : ScriptableObject, IHitEffect
{
    public abstract void Apply(AttackContext context, HitInfo hitInfo, AttackInfo attackInfo);
}
