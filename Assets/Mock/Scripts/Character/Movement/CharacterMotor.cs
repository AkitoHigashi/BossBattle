using System;
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
    /// スキル実行中の回転速度。
    /// </summary>
    private bool _hasOverrideFacingDirection;
    private float _rotationSpeed;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
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
    public void SetOverrideFacingDirection(Vector3 direction,float rotationSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            throw new ArgumentException("有効な回転方向を指定してください。",nameof(direction));

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
    }

    /// <summary>
    /// Overrideを解除し、最後に設定された通常移動速度を再び使用します。
    /// </summary>
    public void ClearOverrideVelocity()
    {
        _overrideVelocity = Vector3.zero;
        _hasOverrideVelocity = false;
    }

    private void FixedUpdate()
    {
        Vector3 horizontal = (_hasOverrideVelocity ? _overrideVelocity : _locomotionVelocity) + _externalVelocity;
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        velocity.y += _pendingExternalY;
        _pendingExternalY = 0f;
        _rigidbody.linearVelocity = velocity;

        //スキル中は回転overrideを優先する
        Vector3 facingDirection = _hasOverrideFacingDirection
            ? _overrideFacingDirection
            : _facingDirection;

        float rotationSpeed = _hasOverrideFacingDirection
            ? _overrideRotationSpeed
            : _rotationSpeed;

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
    }
}
