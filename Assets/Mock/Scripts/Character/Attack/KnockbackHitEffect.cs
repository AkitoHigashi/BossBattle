using UnityEngine;

[CreateAssetMenu(fileName = "KnockbackHitEffect", menuName = "BossBattle/Hit Effect/Knockback")]
public class KnockbackHitEffect : HitEffect
{
    [SerializeField, Min(0f)] private float _power = 5f;
    [SerializeField] private bool _useHitDirection = true;

    public override void Apply(AttackContext context, HitInfo hitInfo, AttackInfo attackInfo)
    {
        if (hitInfo.Target == null || hitInfo.Target.ForceReceiver == null)
            return;

        Vector3 direction = _useHitDirection ? hitInfo.HitDirection : context.AttackDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        hitInfo.Target.ForceReceiver.AddForce(direction.normalized * _power);
    }
}
