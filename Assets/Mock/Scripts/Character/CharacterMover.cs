using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CharacterMover : MonoBehaviour
{
    [SerializeField] private CharacterData _characterData;

    private Rigidbody _rigidbody;
    private Vector3 _moveDirection;

    private void Awake()
    {
        Init();
        if (_characterData == null)
            Debug.LogWarning("CharacterMover requires a CharacterData asset. Assign it in the Inspector.", this);
    }

    public void Init()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void SetMoveDirection(Vector3 direction)
    {
        direction.y = 0f;
        _moveDirection = Vector3.ClampMagnitude(direction, 1f);
    }

    private void FixedUpdate()
    {
        float moveSpeed = _characterData != null ? _characterData.MoveSpeed : 0f;
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = _moveDirection.x * moveSpeed;
        velocity.z = _moveDirection.z * moveSpeed;
        _rigidbody.linearVelocity = velocity;
    }

    private void OnDisable()
    {
        _moveDirection = Vector3.zero;
    }
}

