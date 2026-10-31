using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

/// <summary>
/// 剣の攻撃に合わせて飛び道具を放つ Action（スキル4の波動、スキル6の剣撃）。
/// 中の PlayerAttackAction をそのまま実行し、攻撃判定が開いた瞬間に1発だけ発射する。
/// </summary>
/// <remarks>
/// PlayerAttackAction に飛び道具の機能を足さず、外側から包む形（コンポジション）にした。
/// こうすると飛び道具のない攻撃は今までどおりで、波動の有無は Action の差し替えだけで切り替えられる。
/// 発射タイミングを AnimationEvent（HitStarted）に合わせたのは、Action を順に並べるだけだと
/// 斬撃のアニメーションが終わってから発射され、「斬撃に加えて放つ」見た目にならないため。
/// </remarks>
[CreateAssetMenu(fileName = "ProjectileAttackAction", menuName = "Skills/ProjectileAttackAction")]
public class ProjectileAttackAction : SkillAction
{
    [Tooltip("剣の攻撃。この攻撃判定が開いた瞬間に飛び道具を出す")]
    [SerializeField] private PlayerAttackAction _attack;
    [Tooltip("飛ばす Prefab。SkillProjectile が付いている必要がある")]
    [SerializeField] private SkillProjectile _projectilePrefab;
    [Tooltip("飛ぶ方向")]
    [SerializeField] private MoveDirectionMode _direction = MoveDirectionMode.Forward;
    [Tooltip("キャラクターの足元から見た発射位置（前方向・上方向）")]
    [SerializeField] private Vector2 _spawnOffset = new Vector2(1f, 1f);
    [SerializeField, Min(0.01f)] private float _speed = 20f;
    [Tooltip("飛ぶ距離。「剣先から少し前方まで」なら短め、「前方へ放つ」なら長めにする")]
    [SerializeField, Min(0.01f)] private float _range = 4f;
    [Tooltip("飛び道具が命中したときのダメージ（剣のダメージとは別）")]
    [SerializeField, Min(0)] private int _damage = 5;
    [Tooltip("飛び道具が命中したときのブレイク値（剣のブレイク値とは別）")]
    [SerializeField, Min(0f)] private float _breakValue = 0f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        if (_attack == null)
            throw new InvalidOperationException("ProjectileAttackAction の攻撃が未設定です。");
        if (_projectilePrefab == null)
            throw new InvalidOperationException("ProjectileAttackAction の飛び道具 Prefab が未設定です。");

        AttackAnimationEvents events = context.AnimationEvents;
        bool fired = false;

        // context・execution・fired を使うためローカル関数にする。
        // 名前の付いた関数なので、finally で同じ参照を渡して確実に購読解除できる。
        void OnHitStarted()
        {
            // 1回の攻撃で判定が複数回開くアニメーションでも、発射は1発だけにする。
            if (fired)
                return;
            fired = true;
            Fire(context, execution);
        }

        events.HitStarted += OnHitStarted;
        try
        {
            await _attack.ExecuteAsync(context, execution);
        }
        finally
        {
            // 中断・例外でも購読を外す。残ると後の別の攻撃でも発射されてしまう。
            events.HitStarted -= OnHitStarted;
        }
    }

    private void Fire(SkillContext context, SkillExecution execution)
    {
        Transform owner = context.CharacterMotor.transform;
        Vector3 forward = owner.forward;
        forward.y = 0f;

        Vector3 direction = _direction switch
        {
            MoveDirectionMode.Forward => forward,
            // 長押し中にマウスを動かしても、その時点の照準へ撃つ。
            MoveDirectionMode.AimDirection => execution.CurrentAimDirection,
            MoveDirectionMode.Back => -forward,
            MoveDirectionMode.InputDirection =>
                throw new NotSupportedException("ProjectileAttackActionではInputDirectionは未対応です。"),
            _ => throw new ArgumentOutOfRangeException(nameof(_direction))
        };
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = forward;

        Vector3 position = owner.position + direction.normalized * _spawnOffset.x + Vector3.up * _spawnOffset.y;
        // 毎回生成せず、キャラクターごとのプールから使い回す。
        SkillProjectile projectile = context.ProjectilePool.Get(_projectilePrefab, position, Quaternion.identity);
        // 溜め結果などの倍率は、剣の攻撃と同じく飛び道具にも掛ける。
        AttackData data = execution.ApplyMultipliers(new AttackData(_damage, _breakValue));
        projectile.Launch(direction, _speed, _range, data, owner);
    }
}
