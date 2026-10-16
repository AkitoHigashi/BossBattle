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
        direction.y = 0f;
        if (_duration <= 0f || _speed <= 0f || direction.sqrMagnitude < 0.0001f)
            return;

        try
        {
            Vector3 dic = motor.transform.forward;
            dic.y = 0f;
            switch (_direction)
            {
                case MoveDirectionMode.Forward:

                    motor.SetOverrideVelocity(dic.normalized * _speed);
                    break;
                case MoveDirectionMode.AimDirection:
                    motor.SetOverrideVelocity(direction.normalized * _speed);
                    break;
                case MoveDirectionMode.Back:
                    motor.SetOverrideVelocity(dic.normalized * -1 * _speed);
                    break;
            }

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
