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

    [Header("踏み込み攻撃")]
    [Tooltip("移動中に体が触れた敵へ攻撃するか。アニメーションに関係なく当たるので、攻撃前にすり抜けても取りこぼさない")]
    [SerializeField] private bool _dashHit = false;
    [SerializeField, Min(0)] private int _dashDamage = 10;
    [SerializeField, Min(0f)] private float _dashBreakValue = 0f;
    [Tooltip("当たり判定の半径。0 なら体の太さに合わせる")]
    [SerializeField, Min(0f)] private float _dashHitRadius = 0f;
    [Tooltip("踏み込みで当たった敵には、同じスキルの後の攻撃（斬撃など）を当てない")]
    [SerializeField] private bool _skipLaterHitsOnDashHit = true;

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

            // 1回の物理更新で進む距離。踏み込み攻撃の判定に使う。
            Vector3 step = moveDirection.normalized * (_speed * Time.fixedDeltaTime);
            // 1回の踏み込みで、同じ敵に何度も当てないための記録。
            var dashHitTargets = _dashHit ? new HashSet<IDamageable>() : null;

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                execution.Token.ThrowIfCancellationRequested();
                if (_dashHit)
                    ApplyDashHits(motor, execution, step, dashHitTargets);
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
    /// 今の位置から、次の物理更新で進む先までの範囲にいる敵へ攻撃する。
    /// </summary>
    /// <remarks>
    /// 毎回「次に進む区間」を調べ、区間をつなげて全体を調べる。
    /// 今の位置だけを見ると、速く動いたときに1回の更新で敵を飛び越して取りこぼすため。
    /// </remarks>
    private void ApplyDashHits(CharacterMotor motor, SkillExecution execution, Vector3 step, HashSet<IDamageable> hitTargets)
    {
        Bounds body = motor.GetBodyBounds();
        float radius = _dashHitRadius > 0f ? _dashHitRadius : GetBodyRadius(body);
        Vector3 start = body.center;

        // 溜めなどの倍率は、他の攻撃と同じく掛ける。
        AttackData data = execution.ApplyMultipliers(new AttackData(_dashDamage, _dashBreakValue));
        foreach (Collider hit in Physics.OverlapCapsule(start, start + step, radius, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.transform.IsChildOf(motor.transform))
                continue;
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !hitTargets.Add(target))
                continue;

            data.ApplyTo(target);
            if (_skipLaterHitsOnDashHit)
                execution.ExcludeFromLaterHits(target);
        }
    }

    // 体の太さ（横幅の半分）に少し余裕を足したもの。
    private static float GetBodyRadius(Bounds body)
    {
        return Mathf.Max(body.extents.x, body.extents.z) + 0.1f;
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
        float radius = GetBodyRadius(body);
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
