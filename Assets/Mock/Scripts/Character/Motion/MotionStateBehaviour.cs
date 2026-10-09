using UnityEngine;

public class MotionStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null)
        {
            Debug.LogError("Animator is null in MotionStateBehaviour.OnStateEnter");
            return;
        }
        if (stateInfo.IsTag(AttackTag))
        {
            var motionSystem = animator.GetComponent<IMotionSystem>();
            if (motionSystem != null)
            {
                motionSystem.SetIsMotionActive(true);
            }
        }
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null)
        {
            Debug.LogError("Animator is null in MotionStateBehaviour.OnStateExit");
            return;
        }
        if (stateInfo.IsTag(AttackTag))
        {
            var motionSystem = animator.GetComponent<IMotionSystem>();
            if (motionSystem != null)
            {
                motionSystem.SetIsMotionActive(false);
            }
        }

    }

    private const string AttackTag = "Attack";

}
