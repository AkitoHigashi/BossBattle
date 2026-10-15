using System.Collections.Generic;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    /// <summary>
    /// 攻撃の根元のTransform。攻撃の方向を決定するために使用される。
    /// </summary>
    [SerializeField] private Transform _rootTransform;
    /// <summary>
    /// 攻撃の先端のTransform。攻撃の範囲を決定するために使用される。
    /// </summary>
    [SerializeField] private Transform _tipTransform;
    /// <summary>
    /// 攻撃の当たり判定の半径。
    /// </summary>
    [SerializeField, Min(0.01f)] private float _radius = 0.5f;
    /// <summary>
    /// 攻撃の対象となるレイヤーのマスク。
    /// </summary>
    [SerializeField] private LayerMask _targetLayers = ~0;
    /// <summary>
    /// 攻撃の当たり判定のサンプル数。サンプル数が多いほど、攻撃の範囲が正確に判定されるが、パフォーマンスに影響する可能性がある。
    /// </summary>
    [SerializeField, Min(2)] private int _sampleCount = 3;
    /// <summary>
    /// 攻撃の当たり判定の前回のサンプル位置。攻撃の範囲を決定するために使用される。
    /// </summary>
    private Vector3[] _previous;
    /// <summary>
    /// 攻撃の当たり判定の現在のサンプル位置。攻撃の範囲を決定するために使用される。
    /// </summary>
    private readonly RaycastHit[] _castResults = new RaycastHit[32];
    /// <summary>
    /// 攻撃の当たり判定の重なり判定の結果。攻撃の範囲を決定するために使用される。
    /// </summary>
    private readonly Collider[] _overlapResults = new Collider[32];
    /// <summary>
    /// 攻撃の当たり判定の対象となったダメージ可能なオブジェクトの集合。重複してダメージを与えないようにするために使用される。
    /// </summary>
    private readonly HashSet<IDamageable> _hitTargets = new();
    /// <summary>
    /// 攻撃の根元のTransform。攻撃の方向を決定するために使用される。
    /// </summary>
    private Transform _ownerRoot;
    private int _damage;
    private bool _active;

    public void Init(Transform owner)
    {
        _ownerRoot = owner ?? throw new System.ArgumentNullException(nameof(owner));
        if (_rootTransform == null || _tipTransform == null)
            throw new System.InvalidOperationException("AttackHitboxのBladeRootとBladeTipを設定してください。");
        _sampleCount = Mathf.Max(2, _sampleCount);
        if (_previous == null || _previous.Length != _sampleCount)
            _previous = new Vector3[_sampleCount];
        AbortHit();
    }

    /// <summary>
    /// 攻撃の当たり判定を開始する。攻撃の対象となるオブジェクトにダメージを与えるために使用される。
    /// </summary>
    /// <param name="damage"></param>
    public void BeginHit(int damage)
    {
        if (_previous == null || _ownerRoot == null)
            throw new System.InvalidOperationException("AttackHitbox.Initを先に呼び出してください。");
        _damage = damage;
        _active = true;
        _hitTargets.Clear();
        SaveCurrentPoints();
        CheckOverlap();
    }

    /// <summary>
    /// 攻撃の当たり判定を終了する。攻撃の対象となるオブジェクトにダメージを与える処理を行い、当たり判定を閉じるために使用される。
    /// </summary>
    public void EndHit()
    {
        if (_active) DetectHits();
        AbortHit();
    }

    /// <summary>
    /// 攻撃の当たり判定を中断する。攻撃の対象となるオブジェクトにダメージを与えず、当たり判定を閉じるために使用される。
    /// </summary>
    public void AbortHit()
    {
        _active = false;
        _hitTargets.Clear();
    }

    private void LateUpdate()
    {
        if (_active) DetectHits();
    }

    /// <summary>
    /// 前回の3点から今の3点までをスイープし、前回座標を更新する。
    /// </summary>
    private void DetectHits()
    {
        Vector3 root = _rootTransform.position;
        Vector3 tip = _tipTransform.position;
        for (int i = 0; i < _previous.Length; i++)
        {
            // SampleCount=3ならt=0, 0.5, 1（根元/中央/先端）。
            float t = (float)i / (_previous.Length - 1);
            Vector3 current = Vector3.Lerp(root, tip, t);
            Vector3 delta = current - _previous[i];
            float distance = delta.magnitude;
            if (distance > 0.0001f)
            {
                // 前回→現在の移動方向と距離で球体をスイープする。結果配列を使い回す。
                int count = Physics.SphereCastNonAlloc(
                    _previous[i], _radius, delta / distance,
                    _castResults, distance, _targetLayers,
                    QueryTriggerInteraction.Collide);
                for (int hit = 0; hit < count; hit++)
                    ApplyHit(_castResults[hit].collider);
            }
            _previous[i] = current;
        }
        CheckOverlap();
    }

    /// <summary>
    /// 攻撃の当たり判定の範囲内にあるオブジェクトを検出し、ダメージを与える。
    /// </summary>
    private void CheckOverlap()
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            _rootTransform.position, _tipTransform.position, _radius,
            _overlapResults, _targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            ApplyHit(_overlapResults[i]);
    }

    /// <summary>
    /// 前回のサンプル位置を現在のサンプル位置に更新する。これにより、次回の攻撃判定で正しい範囲が検出される。
    /// </summary>
    private void SaveCurrentPoints()
    {
        Vector3 root = _rootTransform.position;
        Vector3 tip = _tipTransform.position;
        int count = _previous.Length;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            _previous[i] = Vector3.Lerp(root, tip, t);
        }
    }

    /// <summary>
    /// 攻撃の対象となるオブジェクトにダメージを与える。すでにダメージを与えたオブジェクトには再度ダメージを与えない。
    /// </summary>
    /// <param name="collider"></param>
    private void ApplyHit(Collider collider)
    {
        if (collider.transform.IsChildOf(_ownerRoot)) return;
        var damageable = collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            if (_hitTargets.Add(damageable))
            {
                damageable.TakeDamage(_damage);
            }
        }
    }

    private void OnDisable() => AbortHit();

    private void OnValidate()
    {
        _sampleCount = Mathf.Max(2, _sampleCount);
        _radius = Mathf.Max(0.01f, _radius);
    }

    // Sceneビュー選択中のデバッグ表示。実際に掃引した軌跡そのものは描いていない。
    private void OnDrawGizmosSelected()
    {
        if (_rootTransform == null || _tipTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_rootTransform.position, _radius);
        Gizmos.DrawWireSphere(_tipTransform.position, _radius);
        Gizmos.DrawLine(_rootTransform.position, _tipTransform.position);
    }
}
