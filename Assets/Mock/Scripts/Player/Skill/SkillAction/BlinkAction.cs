using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "BlinkAction", menuName = "Skill/Action/BlinkAction")]
public class BlinkAction : SkillAction
{
    [SerializeField] private float _speed = 35f;
    [SerializeField] private float _duration = 0.05f;

    public override async UniTask ExecuteAsync(SkillContext context, CancellationToken cancellationToken)
    {
        CharacterMotor motor = context.CharacterMotor;
        Vector3 direction = motor.transform.forward;
        direction.y = 0f;
        if (_duration <= 0f || _speed <= 0f || direction.sqrMagnitude < 0.0001f)
            return;

        try
        {
            motor.SetOverrideVelocity(direction.normalized * _speed);
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
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
