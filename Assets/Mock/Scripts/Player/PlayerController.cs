using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private SkillDefinition _normalAttack;
    [SerializeField] private SkillDefinition _skill1;
    [SerializeField] private SkillDefinition _dodge;

    [Tooltip("ON: マウス方向を向く / OFF: 移動方向を向く")]
    [SerializeField] private bool _faceMouseCursor = true;

    [SerializeField] private Camera _camera;

    [SerializeField, Header("スキルの受付結果をConsoleに表示するか")] private bool _logSkillResults = false;
    private InputBuffer _inputBuffer;
    private InputAction _moveAction;
    private InputAction _attackAction;
    private InputAction _skill1Action;
    private InputAction _skillDodge;
    private Character _character;
    private CharacterMotor _motor;
    private CharacterData _characterData;
    private SkillSystem _skillSystem;
    private bool _initialized;
    private bool _subscribed;

    public void Initialize(InputBuffer inputBuffer, Character character, CharacterData data, SkillSystem skillSystem = null)
    {
        if (_initialized)
        {
            if (_inputBuffer != inputBuffer || _character != character || _characterData != data || !ReferenceEquals(_skillSystem, skillSystem))
                throw new InvalidOperationException("PlayerControllerを異なる依存関係で再初期化できません。");
            return;
        }

        if (inputBuffer == null)
            throw new ArgumentNullException(nameof(inputBuffer));
        if (character == null)
            throw new ArgumentNullException(nameof(character));
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (!inputBuffer.IsInitialized || inputBuffer.MoveAction == null)
            throw new InvalidOperationException("PlayerControllerより先にInputBufferを初期化してください。");

        if (TryGetComponent<CharacterMotor>(out var motr))
            _motor = motr;

        _inputBuffer = inputBuffer;
        _character = character;
        _characterData = data;
        _skillSystem = skillSystem;
        _moveAction = inputBuffer.MoveAction;
        _attackAction = inputBuffer.AttackAction;
        _skill1Action = inputBuffer.Skill1Action;
        _skillDodge = inputBuffer.DodgeAction;
        _initialized = true;

        if (isActiveAndEnabled)
            Subscribe();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        // マウス方向を向かない設定なら通常移動の回転処理へ戻す
        if (!_faceMouseCursor)
        {
            _motor.ClearAimFacingDirection();
            return;
        }

        if (TryGetMouseDirection(out Vector3 direction))
        {
            // 回転速度はCharacterDataから取得
            _motor.SetAimFactingDirection(direction, _characterData.MouseRotationSpeed);
        }
        else
        {
            // カメラやマウスが利用できないとき
            _motor.ClearAimFacingDirection();
        }
    }

    private void OnEnable()
    {
        if (_initialized)
            Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;
        if (_skillSystem != null)
        {
            if (_normalAttack != null)
            {
                _attackAction.performed += OnAttackPerformed;
                _attackAction.canceled += OnAttackCanceled;
            }
            if (_skill1 != null)
                _skill1Action.started += OnSkill1;
            if (_skillDodge != null)
                _skillDodge.started += OnDodge;
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
                {
                    _attackAction.performed -= OnAttackPerformed;
                    _attackAction.canceled -= OnAttackCanceled;
                }
                if (_skill1 != null)
                    _skill1Action.started -= OnSkill1;
                if (_skillDodge != null)
                    _skillDodge.started -= OnDodge;
            }
            _subscribed = false;
        }
        _motor?.ClearAimFacingDirection();
        _character?.Move(new MovementData(Vector3.zero));
        _skillSystem?.CancelActiveSkill();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirectoin) ? mouseDirectoin : transform.forward;
        _skillSystem.HandleSkillInput(_normalAttack, SkillInputPhase.Pressed, direction);
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirectoin) ? mouseDirectoin : transform.forward;
        _skillSystem.HandleSkillInput(_normalAttack, SkillInputPhase.Released, direction);
    }

    private void OnSkill1(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirectoin) ? mouseDirectoin : transform.forward;
        UseSkill(_skill1, direction);
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;
        direction.y = 0f;
        UseSkill(_dodge, direction.normalized);
    }

    private void UseSkill(SkillDefinition definition, Vector3 direction)
    {
        if (_skillSystem == null || definition == null)
            return;

        // スキルの実行を要求
        bool accepted = _skillSystem.TryUseSkill(definition, direction);

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
    private bool TryGetMouseDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        Camera camera = _camera != null ? _camera : Camera.main;

        if (camera == null || Mouse.current == null)
            return false;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = camera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 targetPoint = ray.GetPoint(distance);
        direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }
}
