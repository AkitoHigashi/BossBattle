using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "BlinkAction", menuName = "Skill/Action/BlinkAction")]
public class BlinkAction : SkillAction
{
    [SerializeField] private MoveDirectionMode _direction;
    [SerializeField] private float _speed = 35f;
    [SerializeField] private float _duration = 0.05f;
    [SerializeField] private float _aimRotationSpeed = 9000f;
    [Tooltip("移動中に敵（ダメージを受けられる相手）をすり抜けるか。地面や壁はすり抜けない")]
    [SerializeField] private bool _passThroughEnemies = false;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        execution.Token.ThrowIfCancellationRequested();
        CharacterMotor motor = context.CharacterMotor;
        // 発動時点の向きを取得
        Vector3 forward = motor.transform.forward;
        forward.y = 0f;

        // 設定された方向モードで移動方向を決定
        // AimDirection は「現在の照準」だが、ブリンクはスキルの最初に同期的に実行されるため、発動時のマウス方向と同じになる。
        Vector3 moveDirection = MoveDirectionResolver.Resolve(_direction, motor, execution);
        if (_duration <= 0f || _speed <= 0f || moveDirection.sqrMagnitude <= 0.0001f)
            return;

        try
        {
            if (_passThroughEnemies)
                motor.BeginPassThrough(FindEnemiesOnPath(motor, moveDirection, _speed * _duration));

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
            // 正常終了・回避の中断・例外のいずれでも、強制移動と向き固定を解除する。
            // 解除しないと Action 終了後も通常の移動・回転へ戻れなくなる。
            motor.ClearOverrideVelocity();
            motor.ClearOverrideFacingDirection();
            if (_passThroughEnemies)
                motor.EndPassThrough();
        }
    }

    /// <summary>
    /// これから通る経路上と、今重なっている位置にいる敵の Collider を集める。
    /// </summary>
    /// <remarks>
    /// 敵用のレイヤーがなく、地面も同じレイヤーにあるため、レイヤー単位ではなく相手ごとにすり抜けを設定する。
    /// 「敵」はダメージを受けられる相手（IDamageable）とし、地面や壁は対象にしない。
    /// </remarks>
    private static List<Collider> FindEnemiesOnPath(CharacterMotor motor, Vector3 direction, float distance)
    {
        // 余裕を持たせ、移動の終わり際にいる相手も含める。
        const float margin = 0.5f;

        Bounds body = motor.GetBodyBounds();
        float radius = Mathf.Max(body.extents.x, body.extents.z) + 0.1f;
        Vector3 start = body.center;
        Vector3 end = start + direction.normalized * (distance + margin);

        var enemies = new List<Collider>();
        foreach (Collider hit in Physics.OverlapCapsule(start, end, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(motor.transform))
                continue;
            if (hit.GetComponentInParent<IDamageable>() != null)
                enemies.Add(hit);
        }
        return enemies;
    }
}
