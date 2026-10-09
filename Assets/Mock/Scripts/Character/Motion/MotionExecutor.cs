using UnityEngine;

public abstract class MotionExecutor : ScriptableObject
{
    public abstract void Execute(MotionContext context);
}
