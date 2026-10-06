using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InputBuffer), typeof(CharacterComposition))]
public class PlayerController : MonoBehaviour
{
    private InputBuffer _inputBuffer;
    private InputAction _moveAction;
    private Character _character;

    private void Start()
    {
        _inputBuffer = GetComponent<InputBuffer>();
        _character = GetComponent<CharacterComposition>().Character;

        SubscribeToMove();
    }

    private void OnEnable()
    {
        SubscribeToMove();
    }

    private void SubscribeToMove()
    {
        if (_character == null || _inputBuffer == null)
            return;

        if (_moveAction != null)
            return;

        _inputBuffer.Init();
        _moveAction = _inputBuffer.MoveAction;

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;

        Vector2 input = _moveAction.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    private void OnDisable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
            _moveAction = null;
        }

        _character?.Move(new MovementData(Vector3.zero));
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }
}
