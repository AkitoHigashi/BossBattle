using UnityEngine;

[CreateAssetMenu(fileName = "InstantAttackMotionExecutor", menuName = "BossBattle/Motion Executor/Instant Attack")]
public class InstantAttackMotionExecutor : MotionExecutor
{
    [SerializeField, Min(0f)] private float _radius = 1f;
    [SerializeField] private LayerMask _targetLayers = ~0;

    public override void Execute(MotionContext context)
    {
        if (context.Owner == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            context.AttackContext.AttackPosition,
            _radius,
            _targetLayers,
            QueryTriggerInteraction.Collide);

        AttackResolver resolver = context.Owner.GetComponent<AttackResolver>();
        if (resolver == null)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            resolver.Resolve(context.AttackContext, hits[i]);
        }

        IMotionSystem motionSystem = context.Owner.GetComponent<IMotionSystem>();
        motionSystem?.ExitMotion();
    }
}
