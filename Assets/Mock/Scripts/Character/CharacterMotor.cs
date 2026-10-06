using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CharacterMotor : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private Vector3 _locomotionVelocity;
    private Vector3 _externalVelocity;
    private float _pendingExternalY;
    private Vector3 _overrideVelocity;
    private bool _hasOverrideVelocity;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void SetLocomotionVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        _locomotionVelocity = velocity;
    }

    
    public void AddExternalVelocity(Vector3 velocity)
    {
        _pendingExternalY += velocity.y;
        velocity.y = 0f;
        _externalVelocity += velocity;
    }

    public void ClearExternalVelocity()
    {
        _externalVelocity = Vector3.zero;
        _pendingExternalY = 0f;
    }

    
    public void SetOverrideVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        _overrideVelocity = velocity;
        _hasOverrideVelocity = true;
    }

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
    }

    private void OnDisable()
    {
        _locomotionVelocity = Vector3.zero;
        ClearExternalVelocity();
        ClearOverrideVelocity();
    }
}
