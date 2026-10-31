using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一定距離をまっすぐ飛び、通り道の相手に1回ずつ命中する飛び道具（波動・剣撃）。
/// 生成側（ProjectileAttackAction）が Launch で向き・攻撃内容・持ち主を渡す。
/// </summary>
/// <remarks>
/// Rigidbody や Collider を使わず、毎フレーム「前回位置→今回位置」を SphereCast で調べる。
/// 速い弾がフレームの間に相手をすり抜けるのを防ぐためで、AttackHitbox と同じ考え方。
/// 貫通弾なので、命中しても消えずに最後まで飛ぶ（同じ相手には1回だけ当たる）。
///
/// ProjectilePool から使い回されるため、飛び終わっても Destroy せずプールへ返す。
/// 再利用時に前回の状態（命中済みの相手など）が残らないよう、Launch で毎回初期化する。
/// </remarks>
[DisallowMultipleComponent]
public class SkillProjectile : MonoBehaviour
{
    [Tooltip("当たり判定の半径")]
    [SerializeField, Min(0.01f)] private float _radius = 0.6f;
    [Tooltip("命中対象のレイヤー")]
    [SerializeField] private LayerMask _targetLayers = ~0;

    private readonly RaycastHit[] _castResults = new RaycastHit[32];
    private readonly Collider[] _overlapResults = new Collider[32];
    // 貫通中に同じ相手へ何度も当たらないよう、命中済みの相手を覚えておく。
    private readonly HashSet<IDamageable> _hitTargets = new();

    private AttackData _attackData;
    private Transform _owner;
    private Vector3 _velocity;
    private float _remainingDistance;
    private bool _launched;
    // プールへ返す処理。プールを通さずに置かれた弾（シーンに直接配置など）では null で、その場合は破棄する。
    private Action<SkillProjectile> _release;

    /// <summary>
    /// 飛び終わったときの返却先を設定する。ProjectilePool が生成時に1回だけ呼ぶ。
    /// </summary>
    public void SetReleaseAction(Action<SkillProjectile> release)
    {
        _release = release;
    }

    /// <summary>
    /// 飛ばし始める。生成直後に1回だけ呼ぶ。
    /// </summary>
    /// <param name="direction">飛ぶ方向（水平に補正する）。</param>
    /// <param name="speed">速度（単位/秒）。</param>
    /// <param name="range">飛ぶ距離。ここまで進むと消える。</param>
    /// <param name="attackData">命中した相手に与える内容。</param>
    /// <param name="owner">撃った本人。自分自身には当たらないようにする。</param>
    public void Launch(Vector3 direction, float speed, float range, AttackData attackData, Transform owner)
    {
        direction.y = 0f;
        // 前回の発射で命中した相手を忘れる。残っていると、再利用した弾が同じ相手に当たらなくなる。
        _hitTargets.Clear();

        if (direction.sqrMagnitude <= 0.0001f || speed <= 0f || range <= 0f)
        {
            Despawn();
            return;
        }

        direction.Normalize();
        _velocity = direction * speed;
        _remainingDistance = range;
        _attackData = attackData;
        _owner = owner;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        _launched = true;

        // 出現位置に最初から重なっている相手にも当てる（密着状態で撃った場合）。
        CheckOverlap();
    }

    private void Update()
    {
        if (!_launched)
            return;

        float step = Mathf.Min(_velocity.magnitude * Time.deltaTime, _remainingDistance);
        Vector3 direction = _velocity.normalized;

        // 移動する前に、これから通る区間を調べる。
        int count = Physics.SphereCastNonAlloc(
            transform.position, _radius, direction, _castResults, step,
            _targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            ApplyHit(_castResults[i].collider);

        transform.position += direction * step;
        _remainingDistance -= step;

        if (_remainingDistance <= 0f)
            Despawn();
    }

    /// <summary>
    /// 飛び終わった弾を片付ける。プール経由ならプールへ返し、そうでなければ破棄する。
    /// </summary>
    private void Despawn()
    {
        // 返却後に Update が走っても動かないよう、先に止める。
        _launched = false;
        _owner = null;

        if (_release != null)
            _release(this);
        else
            Destroy(gameObject);
    }

    private void CheckOverlap()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, _radius, _overlapResults, _targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            ApplyHit(_overlapResults[i]);
    }

    private void ApplyHit(Collider collider)
    {
        // 撃った本人（とその子オブジェクト）には当てない。
        if (_owner != null && collider.transform.IsChildOf(_owner))
            return;

        var damageable = collider.GetComponentInParent<IDamageable>();
        if (damageable != null && _hitTargets.Add(damageable))
            _attackData.ApplyTo(damageable);
    }

    // Sceneビュー選択中のデバッグ表示。
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
