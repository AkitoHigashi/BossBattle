using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum MoveDirectionMode
{
    Forward,
    AimDirection,
    Back
}

[CreateAssetMenu(fileName = "BlinkAction", menuName = "Skill/Action/BlinkAction")]
public class BlinkAction : SkillAction
{
    [SerializeField] private MoveDirectionMode _direction;
    [SerializeField] private float _speed = 35f;
    [SerializeField] private float _duration = 0.05f;

    public override async UniTask ExecuteAsync(SkillContext context, CancellationToken cancellationToken, Vector3 direction)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CharacterMotor motor = context.CharacterMotor;
        Vector3 moveDirection = _direction switch
        {
            MoveDirectionMode.Forward => motor.transform.forward,

            MoveDirectionMode.AimDirection => direction,

            MoveDirectionMode.Back => -motor.transform.forward,

            _ => Vector3.zero
        };

        moveDirection.y = 0f;
        if (_duration <= 0f || _speed <= 0f || direction.sqrMagnitude < 0.0001f)
            return;

        moveDirection.y = 0f;
        try
        {
            motor.SetOverrideVelocity(moveDirection.normalized * _speed);
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                elapsed += Time.fixedDeltaTime;
            }

        }
        finally
        {
            motor.ClearOverrideVelocity();
        }
    }
}
