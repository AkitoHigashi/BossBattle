using UnityEngine;

public readonly struct MotionContext
{
    public MotionContext(
        MonoBehaviour owner,
        Character actor,
        AttackContext attackContext,
        MotionData motionData)
    {
        Owner = owner;
        Actor = actor;
        AttackContext = attackContext;
        MotionData = motionData;
    }

    public readonly MonoBehaviour Owner;
    public readonly Character Actor;
    public readonly AttackContext AttackContext;
    public readonly MotionData MotionData;
}
