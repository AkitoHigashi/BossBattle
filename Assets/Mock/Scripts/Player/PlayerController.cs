using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputBuffer _inputBuffer;
    private InputAction _moveAction;
    private Character _character;
    private bool _initialized;
    private bool _subscribed;
    [SerializeField] 
    private AttackDefinition[] _attackDefinition;
    public void Initialize(InputBuffer inputBuffer, Character character)
    {
        if (_initialized)
        {
            if (_inputBuffer != inputBuffer || _character != character)
                throw new InvalidOperationException("PlayerControllerは異なる依存関係で再初期化できません。");
            return;
        }

        if (inputBuffer == null)
            throw new ArgumentNullException(nameof(inputBuffer));
        if (character == null)
            throw new ArgumentNullException(nameof(character));
        if (!inputBuffer.IsInitialized || inputBuffer.MoveAction == null)
            throw new InvalidOperationException("PlayerControllerより先にInputBufferを初期化してください。");

        _inputBuffer = inputBuffer;
        _character = character;
        _moveAction = inputBuffer.MoveAction;
        _initialized = true;

        if (isActiveAndEnabled)
            SubscribeToMove();
    }

    private void OnEnable()
    {
        if (_initialized)
            SubscribeToMove();
    }

    private void SubscribeToMove()
    {
        if (_subscribed)
            return;

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;
        _subscribed = true;

        Vector2 input = _moveAction.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    private void OnDisable()
    {
        if (_subscribed)
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
            _subscribed = false;
        }

        _character?.Move(new MovementData(Vector3.zero));
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            AttackData attackData = new AttackData(_attackDefinition[0]); 
            _character.Attack(attackData);
        }
    }
}
