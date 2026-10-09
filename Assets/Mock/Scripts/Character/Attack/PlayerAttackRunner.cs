
using UnityEngine;

public class PlayerAttackRunner : IAttackRunner
{
    public PlayerAttackRunner(Character attacker, IMotionSystem motionSystem, MonoBehaviour owner)
    {
        _attacker = attacker;
        _motionSystem = motionSystem ?? throw new System.ArgumentNullException(nameof(motionSystem));
        _owner = owner;
    }

    public void Running(AttackData attackData)
    {
        var attackContext = new AttackContext(
            _attacker,
            attackData,
            GetAttackPosition(),
            GetAttackDirection());
        var motionContext = new MotionContext(_owner, _attacker, attackContext, attackData.MotionData);
        _motionSystem.EnterMotion(motionContext);
    }

    private Vector3 GetAttackPosition()
    {
        return _owner != null ? _owner.transform.position : Vector3.zero;
    }

    private Vector3 GetAttackDirection()
    {
        return _owner != null ? _owner.transform.forward : Vector3.forward;
    }

    private readonly Character _attacker;
    private readonly IMotionSystem _motionSystem;
    private readonly MonoBehaviour _owner;
}

