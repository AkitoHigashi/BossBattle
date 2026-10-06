using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InputBuffer), typeof(CharacterMover))]
public class PlayerController : MonoBehaviour
{
    private InputBuffer _inputBuffer;
    private CharacterMover _mover;

    private void Awake()
    {
        _inputBuffer = GetComponent<InputBuffer>();
        _mover = GetComponent<CharacterMover>();
        _inputBuffer.Init();
        _mover.Init();
    }

    private void OnEnable()
    {
        _inputBuffer.MoveAction.performed += OnMove;
        _inputBuffer.MoveAction.canceled += OnMove;
    }

    private void OnDisable()
    {
        if (_inputBuffer?.MoveAction != null)
        {
            _inputBuffer.MoveAction.performed -= OnMove;
            _inputBuffer.MoveAction.canceled -= OnMove;
        }

        _mover.SetMoveDirection(Vector3.zero);
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        _mover.SetMoveDirection(
            new Vector3(input.x, 0f, input.y)
        );
    }

}

