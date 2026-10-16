using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private SkillDefinition _normalAttack;
    [SerializeField] private SkillDefinition _skill1;

    [SerializeField, Header("スキルの受付結果をConsoleに表示するか")] private bool _logSkillResults = false;
    private InputBuffer _inputBuffer;
    private InputAction _moveAction;
    private InputAction _attackAction;
    private InputAction _skill1Action;
    private Character _character;
    private SkillSystem _skillSystem;
    private bool _initialized;
    private bool _subscribed;

    public void Initialize(InputBuffer inputBuffer, Character character, SkillSystem skillSystem = null)
    {
        if (_initialized)
        {
            if (_inputBuffer != inputBuffer || _character != character || !ReferenceEquals(_skillSystem, skillSystem))
                throw new InvalidOperationException("PlayerControllerを異なる依存関係で再初期化できません。");
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
        _skillSystem = skillSystem;
        _moveAction = inputBuffer.MoveAction;
        _attackAction = inputBuffer.AttackAction;
        _skill1Action = inputBuffer.Skill1Action;
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
        if (_skillSystem != null)
        {
            if (_normalAttack != null)
                _attackAction.started += OnAttack;
            if (_skill1 != null)
                _skill1Action.started += OnSkill1;
        }
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
            if (_skillSystem != null)
            {
                if (_normalAttack != null)
                    _attackAction.started -= OnAttack;
                if (_skill1 != null)
                    _skill1Action.started -= OnSkill1;
            }
            _subscribed = false;
        }

        _character?.Move(new MovementData(Vector3.zero));
        _skillSystem?.CancelActiveSkill();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        UseSkill(_normalAttack);
    }

    private void OnSkill1(InputAction.CallbackContext context)
    {
        UseSkill(_skill1);
    }

    /// <summary>
    /// SkillSystemへスキルの実行を要求する。
    /// </summary>
    private void UseSkill(SkillDefinition definition)
    {
        if (_skillSystem == null || definition == null)
            return;

        // スキルの実行を要求
        bool accepted = _skillSystem.TryUseSkill(definition, GetMOuseDirection());

        // テスト時のみ結果をログ出力
        if (_logSkillResults)
        {
            float cooldown = _skillSystem.GetCooldownRemaing(definition);

            Debug.Log(
                $"[PlayerController] {definition.SkillName}: " +
                $"accepted={accepted}, cooldown={cooldown:0.00}s",
                this
            );
        }
    }

    /// <summary>
    /// マウスカーソルが指すワールド方向を取得する
    /// </summary>
    /// <returns></returns>
    private Vector3 GetMOuseDirection()
    {
        if (Camera.main == null || Mouse.current == null)
            return transform.forward;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (!plane.Raycast(ray, out float distance))
            return transform.forward;

        Vector3 targetPoint = ray.GetPoint(distance);
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return transform.forward;

        return direction.normalized;
    }
}
