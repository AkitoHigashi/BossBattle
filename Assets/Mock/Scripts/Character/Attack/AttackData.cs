
using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct AttackData
{
    public AttackData(
        AttackDefinition definition,
        MotionData motionData,
        int baseDamage,
        IDamageCalculation damageCalculation,
        IReadOnlyList<IHitEffect> hitEffects)
    {
        Definition = definition;
        MotionData = motionData;
        BaseDamage = Mathf.Max(0, baseDamage);
        DamageCalculation = damageCalculation;
        HitEffects = hitEffects ?? Array.Empty<IHitEffect>();
    }

    public AttackData(AttackDefinition definition)
        : this(
            definition,
            definition != null ? definition.MotionData : default,
            definition != null ? definition.BaseDamage : 0,
            definition != null ? definition.DamageCalculation : null,
            definition != null ? definition.HitEffects : Array.Empty<IHitEffect>())
    {
    }

    public AttackDefinition Definition { get; }
    public MotionData MotionData { get; }
    public int BaseDamage { get; }
    public IDamageCalculation DamageCalculation { get; }
    public IReadOnlyList<IHitEffect> HitEffects { get; }
}
