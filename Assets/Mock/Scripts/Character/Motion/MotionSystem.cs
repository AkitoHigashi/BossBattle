using UnityEngine;

public class MotionSystem : MonoBehaviour, IMotionSystem
{
    public void Initialize()
    {
       _animator = GetComponent<Animator>();
       _isMotionActive = false;
    }

    public void EnterMotion(MotionData motionData)
    {
        EnterMotion(new MotionContext(this, null, default, motionData));
    }

    public void EnterMotion(MotionContext motionContext)
    {
        if (_isMotionActive && !motionContext.MotionData.CanCancel)
            return;

        _isMotionActive = true;
        _currentMotion = motionContext.MotionData;
        _attackResolver = GetComponent<AttackResolver>();
        if (_attackResolver != null && motionContext.AttackContext.Attacker != null)
            _attackResolver.BeginAttack(motionContext.AttackContext);

        MotionExecutor executor = _currentMotion.Executor;
        if (executor != null)
        {
            executor.Execute(motionContext);
            if (_currentMotion.Duration > 0f)
                Invoke(nameof(ExitMotion), _currentMotion.Duration);
            return;
        }

        PlayAnimationTrigger(_currentMotion);

        if (_currentMotion.Duration > 0f)
            Invoke(nameof(ExitMotion), _currentMotion.Duration);
    }

    public void ExitMotion()
    {
        CancelInvoke(nameof(ExitMotion));
        _currentMotion = default;
        _isMotionActive = false;
        if (_attackResolver != null)
            _attackResolver.EndAttack();
    }

    public bool GetIsMotionActive()
    {
        return _isMotionActive;
    }

    public void SetIsMotionActive(bool isActive)
    {
        _isMotionActive = isActive;
    }

    private void PlayAnimationTrigger(MotionData motionData)
    {
        if (_animator == null || string.IsNullOrEmpty(motionData.AnimatorTriggerName))
            return;

        _animator.SetTrigger(motionData.AnimatorTriggerName);
    }

    private Animator _animator;
    private bool _isMotionActive;
    private MotionData _currentMotion;
    private AttackResolver _attackResolver;

}
