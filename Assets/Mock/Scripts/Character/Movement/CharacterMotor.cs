using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CharacterMotor : MonoBehaviour, IForceReceiver
{
    private Rigidbody _rigidbody;
    /// <summary>
    /// 通常移動のワールド座標系XZ速度。Overrideが有効でない場合に使用します。
    /// </summary>
    private Vector3 _locomotionVelocity;
    /// <summary>
    /// 通常移動またはOverrideに加算する外部XZ速度。明示的な解除まで保持します。
    /// </summary>
    private Vector3 _externalVelocity;
    /// <summary>
    /// 次のFixedUpdateで現在のY速度に一度だけ加算する、未適用の外部Y速度。
    /// </summary>
    private float _pendingExternalY;
    /// <summary>
    /// 通常移動のXZ速度を置き換える速度。外部速度の加算と現在のY速度は維持します。
    /// </summary>
    private Vector3 _overrideVelocity;
    private bool _hasOverrideVelocity;
    /// <summary>
    /// ワールド座標系の水平な目標方向。現在の向きや回転角そのものではありません。
    /// </summary>
    private Vector3 _facingDirection;
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
        _facingDirection = direction;
        _rotationSpeed = Mathf.Max(0f, rotationSpeed);
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

    public void AddForce(Vector3 force)
    {
        AddExternalVelocity(force);
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
        Vector3 horizontal = (_hasOverrideVelocity ? _overrideVelocity : _locomotionVelocity)
            + _externalVelocity;
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        velocity.y += _pendingExternalY;
        _pendingExternalY = 0f;
        _rigidbody.linearVelocity = velocity;

        if (_facingDirection.sqrMagnitude <= 0.0001f || _rotationSpeed <= 0f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(_facingDirection, Vector3.up);
        Quaternion rotation = Quaternion.RotateTowards(
               _rigidbody.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(rotation);

    }

    private void OnDisable()
    {
        _locomotionVelocity = Vector3.zero;
        _facingDirection = Vector3.zero;
        ClearExternalVelocity();
        ClearOverrideVelocity();
    }
}
