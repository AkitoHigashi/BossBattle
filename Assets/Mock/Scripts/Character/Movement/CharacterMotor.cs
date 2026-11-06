using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CharacterMotor : MonoBehaviour
{
    private Rigidbody _rigidbody;
    /// <summary>
    /// 通常移動速度
    /// </summary>
    private Vector3 _locomotionVelocity;
    /// <summary>
    /// ノックバックなどの外部速度
    /// </summary>
    private Vector3 _externalVelocity;
    /// <summary>
    /// 次のFixedUpdateで現在のY速度に一度だけ加算する、未適用の外部Y速度。
    /// </summary>
    private float _pendingExternalY;
    /// <summary>
    /// スキルなどによる移動速度のOverride
    /// </summary>
    private Vector3 _overrideVelocity;
    private bool _hasOverrideVelocity;
    /// <summary>
    /// 通常移動時の回転方向・回転速度
    /// </summary>
    private Vector3 _facingDirection;
    /// <summary>
    /// スキル実行中に優先する回転方向。
    /// </summary>
    private Vector3 _overrideFacingDirection;
    /// <summary>
    /// スキル実行中の回転速度。
    /// 0の場合は現在の向きを維持する。
    /// </summary>
    private float _overrideRotationSpeed;
    /// <summary>
    /// スキルによる回転方向の上書きが有効か。
    /// </summary>
    private bool _hasOverrideFacingDirection;

    /// <summary>
    /// マウス照準で向く方向。スキルの上書きより優先度が低く、通常移動より高い。
    /// </summary>
    private Vector3 _aimFacingDirection;
    /// <summary>
    /// マウス照準へ向くときの回転速度（度/秒）。
    /// </summary>
    private float _aimRotationSpeed;
    /// <summary>
    /// マウス照準による回転が有効か。
    /// </summary>
    private bool _hasAimFacingDirection;
    /// <summary>
    /// 通常移動（歩き）を止めているか。スキル中に歩けない設定のときに使う。
    /// Override と違い、スキル自身の移動（SetOverrideVelocity）は止めない。
    /// </summary>
    private bool _isLocomotionLocked;

    // すり抜け用：自分の体の Collider と、今すり抜けている相手の Collider
    private Collider[] _bodyColliders = Array.Empty<Collider>();
    private readonly HashSet<Collider> _passThroughTargets = new();
    private readonly List<Collider> _restoreBuffer = new();
    private bool _isPassingThrough;
    /// <summary>
    /// 通常移動の向きへ回転するときの速度（度/秒）。
    /// </summary>
    private float _rotationSpeed;
    /// <summary>
    /// 時間制限付き速度Overrideの残り秒数。負の値は「時間制限なし（解除されるまで続く）」を表す。
    /// </summary>
    private float _overrideVelocityRemaining = -1f;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        CollectBodyColliders();
    }

    /// <summary>
    /// 通常移動のXZ速度を保持し、FixedUpdateでの速度合成に使用します。
    /// </summary>
    /// <param name="velocity">ワールド座標系の速度（単位/秒）。Y成分は無視します。</param>
    public void SetLocomotionVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        _locomotionVelocity = velocity;
    }

    /// <summary>
    /// FixedUpdateで回転するための水平な目標方向と旋回速度を保持します。
    /// </summary>
    /// <param name="direction">ワールド座標系の目標方向。Y成分は無視します。</param>
    /// <param name="rotationSpeed">旋回速度（度/秒）。負値は0に制限します。</param>
    public void SetFacingDirection(Vector3 direction, float rotationSpeed)
    {
        direction.y = 0f;

        // 入力がゼロなら新しい方向を設定しない
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        _facingDirection = direction.normalized;
        _rotationSpeed = Mathf.Max(0f, rotationSpeed);
    }

    /// <summary>
    /// スキル実行中の回転方向を一時的に上書きする。
    /// </summary>
    public void SetOverrideFacingDirection(Vector3 direction, float rotationSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            throw new ArgumentException("有効な回転方向を指定してください。", nameof(direction));

        _overrideFacingDirection = direction.normalized;
        _overrideRotationSpeed = Mathf.Max(0f, rotationSpeed);
        _hasOverrideFacingDirection = true;
    }

    /// <summary>
    /// 外部速度を加算します。XZは解除まで保持し、Yは次のFixedUpdateで一度だけ加算します。
    /// </summary>
    /// <param name="velocity">加算するワールド座標系の速度（単位/秒）。力ではありません。</param>
    public void AddExternalVelocity(Vector3 velocity)
    {
        _pendingExternalY += velocity.y;
        velocity.y = 0f;
        _externalVelocity += velocity;
    }

    /// <summary>
    /// 回転方向のOverrideを解除する。
    /// 通常移動の回転制御に戻る。
    /// </summary>
    public void ClearOverrideFacingDirection()
    {
        _overrideFacingDirection = Vector3.zero;
        _overrideRotationSpeed = 0f;
        _hasOverrideFacingDirection = false;
    }

    /// <summary>
    /// 保持中の外部XZ速度と未適用のY加算を解除します。適用済みのY速度は変更しません。
    /// </summary>
    public void ClearExternalVelocity()
    {
        _externalVelocity = Vector3.zero;
        _pendingExternalY = 0f;
    }

    /// <summary>
    /// 通常移動のXZ速度を置き換えます。外部速度は引き続き加算します。
    /// </summary>
    /// <param name="velocity">置き換えるワールド座標系の速度（単位/秒）。Y成分は無視します。</param>
    public void SetOverrideVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        _overrideVelocity = velocity;
        _hasOverrideVelocity = true;
        _overrideVelocityRemaining = -1f;
    }

    /// <summary>
    /// 指定時間だけ移動速度を上書きする。
    /// durationは秒単位。
    /// </summary>
    public void SetOverrideVelocity(Vector3 velocity, float duration)
    {
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        if (duration == 0f)
        {
            ClearOverrideVelocity();
            return;
        }

        SetOverrideVelocity(velocity);
        _overrideVelocityRemaining = duration;
    }

    /// <summary>
    /// Overrideを解除し、最後に設定された通常移動速度を再び使用します。
    /// </summary>
    public void ClearOverrideVelocity()
    {
        _overrideVelocity = Vector3.zero;
        _hasOverrideVelocity = false;
        _overrideVelocityRemaining = -1f;
    }

    /// <summary>
    /// プレイヤーの照準方向を設定する。
    /// </summary>
    public void SetAimFactingDirection(Vector3 direction, float rotationnSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        _aimFacingDirection = direction.normalized;
        _aimRotationSpeed = Mathf.Max(0f, rotationnSpeed);
        _hasAimFacingDirection = true;
    }

    /// <summary>
    /// マウス照準による回転制御を解除する。
    /// </summary>
    public void ClearAimFacingDirection()
    {
        _aimFacingDirection = Vector3.zero;
        _aimRotationSpeed = 0f;
        _hasAimFacingDirection = false;
    }

    /// <summary>
    /// 指定した相手との衝突を無効にし、すり抜けられるようにする。EndPassThrough まで続く。
    /// </summary>
    public void BeginPassThrough(IEnumerable<Collider> targets)
    {
        foreach (Collider target in targets)
        {
            if (target == null || !_passThroughTargets.Add(target))
                continue;
            foreach (Collider body in _bodyColliders)
                Physics.IgnoreCollision(body, target, true);
        }
        _isPassingThrough = true;
    }

    /// <summary>
    /// すり抜けを終える。衝突は、相手と重ならなくなったものから順に元へ戻す。
    /// </summary>
    /// <remarks>
    /// 相手の中にいるうちに衝突を戻すと、物理エンジンが体を押し出し、来た方向へ弾き戻されることがある。
    /// そのため、重なりが解けるまで（体が抜け出るまで）は無効のままにしておく。
    /// </remarks>
    public void EndPassThrough()
    {
        _isPassingThrough = false;
    }

    /// <summary>
    /// 自分の体の Collider 全体を囲む範囲。すり抜ける相手を探すときに使う。
    /// </summary>
    public Bounds GetBodyBounds()
    {
        if (_bodyColliders.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        Bounds bounds = _bodyColliders[0].bounds;
        for (int i = 1; i < _bodyColliders.Length; i++)
            bounds.Encapsulate(_bodyColliders[i].bounds);
        return bounds;
    }

    /// <summary>
    /// 通常移動（歩き）を止める・再開する。止めている間も入力された速度は保持し、再開するとすぐ歩き出せる。
    /// </summary>
    public void SetLocomotionLocked(bool locked)
    {
        _isLocomotionLocked = locked;
    }

    private void FixedUpdate()
    {
        if (!_isPassingThrough && _passThroughTargets.Count > 0)
            RestorePassThrough(force: false);

        Vector3 locomotion = _isLocomotionLocked ? Vector3.zero : _locomotionVelocity;
        Vector3 horizontal = (_hasOverrideVelocity ? _overrideVelocity : locomotion) + _externalVelocity;
        Vector3 velocity = _rigidbody.linearVelocity;
        // 時間制限付きOverrideの残り時間を更新
        if (_hasOverrideVelocity && _overrideVelocityRemaining >= 0f)
        {
            _overrideVelocityRemaining -= Time.fixedDeltaTime;

            if (_overrideVelocityRemaining <= 0f)
            {
                ClearOverrideVelocity();
            }
        }
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        velocity.y += _pendingExternalY;
        _pendingExternalY = 0f;
        _rigidbody.linearVelocity = velocity;

        Vector3 facingDirection;
        float rotationSpeed;

        // 優先度1：スキル
        if (_hasOverrideFacingDirection)
        {
            facingDirection = _overrideFacingDirection;
            rotationSpeed = _overrideRotationSpeed;
        }
        // 優先度2：マウス照準
        else if(_hasAimFacingDirection)
        {
            facingDirection = _aimFacingDirection;
            rotationSpeed = _aimRotationSpeed;
        }
        // 優先度3：通常移動
        else
        {
            facingDirection = _facingDirection;
            rotationSpeed = _rotationSpeed;
        }

        if (facingDirection.sqrMagnitude <= 0.0001f || rotationSpeed <= 0f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        Quaternion rotation = Quaternion.RotateTowards(
               _rigidbody.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(rotation);
    }

    private void OnDisable()
    {
        _locomotionVelocity = Vector3.zero;
        _facingDirection = Vector3.zero;
        ClearExternalVelocity();
        ClearOverrideVelocity();
        ClearOverrideFacingDirection();
        ClearAimFacingDirection();
        _isLocomotionLocked = false;
        // 無効化されたら、重なっていても衝突を必ず戻す（無視したまま残らないようにする）。
        _isPassingThrough = false;
        RestorePassThrough(force: true);
    }

    private void CollectBodyColliders()
    {
        var colliders = new List<Collider>();
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            // トリガーは物理的な衝突に関わらないので対象外。
            if (!collider.isTrigger)
                colliders.Add(collider);
        }
        _bodyColliders = colliders.ToArray();
    }

    /// <summary>
    /// すり抜けていた相手との衝突を元に戻す。force が false なら、まだ重なっている相手はそのままにする。
    /// </summary>
    private void RestorePassThrough(bool force)
    {
        _restoreBuffer.Clear();
        foreach (Collider target in _passThroughTargets)
        {
            // 破棄された相手は、戻す必要がないので一覧から外すだけ。
            if (target == null || force || !IsOverlapping(target))
                _restoreBuffer.Add(target);
        }

        foreach (Collider target in _restoreBuffer)
        {
            _passThroughTargets.Remove(target);
            if (target == null)
                continue;
            foreach (Collider body in _bodyColliders)
                Physics.IgnoreCollision(body, target, false);
        }
    }

    private bool IsOverlapping(Collider target)
    {
        foreach (Collider body in _bodyColliders)
        {
            if (Physics.ComputePenetration(
                    body, body.transform.position, body.transform.rotation,
                    target, target.transform.position, target.transform.rotation,
                    out _, out _))
                return true;
        }
        return false;
    }
}
