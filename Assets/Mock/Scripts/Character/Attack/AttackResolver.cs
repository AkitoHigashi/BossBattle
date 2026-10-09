using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AttackResolver : MonoBehaviour
{
    public void BeginAttack(AttackContext context)
    {
        _currentContext = context;
        _hasCurrentContext = true;
        _hitTargets.Clear();
    }

    public void EndAttack()
    {
        _hasCurrentContext = false;
        _hitTargets.Clear();
    }

    public void Resolve(Collider targetCollider)
    {
        if (!_hasCurrentContext)
            return;

        Resolve(_currentContext, targetCollider);
    }

    public void Resolve(AttackContext context, Collider targetCollider)
    {
        if (targetCollider == null)
            return;

        CharacterComposition targetComposition = targetCollider.GetComponentInParent<CharacterComposition>();
        if (targetComposition == null || targetComposition.Character == null)
            return;

        Character target = targetComposition.Character;
        if (target == context.Attacker)
            return;

        bool allowMultipleHits = context.AttackData.Definition != null
            && context.AttackData.Definition.AllowMultipleHitsPerActivation;
        if (!allowMultipleHits && _hitTargets.Contains(target))
            return;

        _hitTargets.Add(target);

        Vector3 hitDirection = targetCollider.transform.position - context.AttackPosition;
        var hitInfo = new HitInfo(target, targetCollider.ClosestPoint(context.AttackPosition), hitDirection, targetCollider);
        IDamageCalculation damageCalculation = context.AttackData.DamageCalculation;
        int damage = damageCalculation != null
            ? damageCalculation.CalculateDamage(context, hitInfo)
            : Mathf.Max(0, context.AttackData.BaseDamage);

        target.TakeDamage(damage);

        var attackInfo = new AttackInfo(damage);
        var hitEffects = context.AttackData.HitEffects;
        for (int i = 0; i < hitEffects.Count; i++)
        {
            hitEffects[i]?.Apply(context, hitInfo, attackInfo);
        }
    }

    private readonly HashSet<Character> _hitTargets = new HashSet<Character>();
    private AttackContext _currentContext;
    private bool _hasCurrentContext;
}
