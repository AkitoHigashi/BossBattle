using UnityEngine;

[CreateAssetMenu(fileName = "AnimationMotionExecutor", menuName = "BossBattle/Motion Executor/Animation")]
public class AnimationMotionExecutor : MotionExecutor
{
    public override void Execute(MotionContext context)
    {
        if (context.Owner == null)
            return;

        Animator animator = context.Owner.GetComponent<Animator>();
        if (animator == null)
            return;

        string triggerName = context.MotionData.AnimatorTriggerName;
        if (!string.IsNullOrEmpty(triggerName))
            animator.SetTrigger(triggerName);
    }
}
