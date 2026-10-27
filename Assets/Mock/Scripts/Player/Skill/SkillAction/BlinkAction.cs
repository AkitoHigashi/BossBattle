using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum MoveDirectionMode
{
    Forward,
    AimDirection,
    Back,
    InputDirection
}

[CreateAssetMenu(fileName = "BlinkAction", menuName = "Skill/Action/BlinkAction")]
public class BlinkAction : SkillAction
{
    [SerializeField] private MoveDirectionMode _direction;
    [SerializeField] private float _speed = 35f;
    [SerializeField] private float _duration = 0.05f;
    [SerializeField] private float _aimRotationSpeed = 9000f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        execution.Token.ThrowIfCancellationRequested();
        CharacterMotor motor = context.CharacterMotor;
        // 発動時点の向きを取得
        Vector3 forward = motor.transform.forward;
        forward.y = 0f;

        // 設定された方向モードで移動方向を決定
        Vector3 moveDirection = _direction switch
        {
            MoveDirectionMode.Forward => forward,
            MoveDirectionMode.AimDirection => execution.Direction,
            MoveDirectionMode.Back => -forward,
            MoveDirectionMode.InputDirection => execution.Direction,
            _ => Vector3.zero
        };

        moveDirection.y = 0f;
        if (_duration <= 0f || _speed <= 0f || moveDirection.sqrMagnitude <= 0.0001f)
            return;

        try
        {
            motor.SetOverrideVelocity(moveDirection.normalized * _speed);
            if (_direction == MoveDirectionMode.AimDirection)
            {
                // マウス方向へのブリンク
                motor.SetOverrideFacingDirection(moveDirection, _aimRotationSpeed);
            }
            else
            {
                // 前方回避・後方回避では向きを固定
                motor.SetOverrideFacingDirection(forward, 0f);
            }

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                execution.Token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, execution.Token);
                elapsed += Time.fixedDeltaTime;
            }

        }
        finally
        {
            motor.ClearOverrideVelocity();
            motor.ClearOverrideFacingDirection();
        }
    }
}
