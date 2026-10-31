using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーの入力を受け取り、移動とスキルの要求へ変換する。
/// スキルの中身や実行管理は持たず、「どのスキルを・どの方向へ・押したか離したか」を SkillSystem へ渡すだけにしている。
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Skill")]
    [Tooltip("通常攻撃（左クリック）")]
    [SerializeField] private SkillDefinition _normalAttack;
    [Tooltip("3連撃（右クリック）")]
    [SerializeField] private SkillDefinition _skill1;
    [Tooltip("ブリンク斬り（単発）")]
    [SerializeField] private SkillDefinition _skill2;
    [Tooltip("踏み込み斬りつけ（単発）")]
    [SerializeField] private SkillDefinition _skill3;
    [Tooltip("前方連続突き（長押し）")]
    [SerializeField] private SkillDefinition _skill4;
    [Tooltip("溜め3連（長押し）")]
    [SerializeField] private SkillDefinition _skill5;
    [Tooltip("溜め斬りつけ（長押し）")]
    [SerializeField] private SkillDefinition _skill6;
    [Tooltip("回避。実行中のスキルに割り込める")]
    [SerializeField] private SkillDefinition _dodge;

    [Tooltip("ON: マウス方向を向く / OFF: 移動方向を向く")]
    [SerializeField] private bool _faceMouseCursor = true;

    [Tooltip("マウス方向の計算に使うカメラ。未設定なら Camera.main を使う")]
    [SerializeField] private Camera _camera;

    [SerializeField, Header("スキルの受付結果をConsoleに表示するか")] private bool _logSkillResults = false;
    private InputBuffer _inputBuffer;
    private InputAction _moveAction;
    private InputAction _attackAction;
    private InputAction _skill1Action;
    private InputAction _skill2Action;
    private InputAction _skill3Action;
    private InputAction _skill4Action;
    private InputAction _skill5Action;
    private InputAction _skill6Action;
    private InputAction _skillDodge;
    private Character _character;
    private CharacterMotor _motor;
    private CharacterData _characterData;
    private SkillSystem _skillSystem;
    // 回避の割り込み要求（後片付け待ちがある非同期処理）を、無効化時にまとめて取り消すためのもの。
    private CancellationTokenSource _skillRequestCts;
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
        _skill2Action = inputBuffer.Skill2Action;
        _skill3Action = inputBuffer.Skill3Action;
        _skill4Action = inputBuffer.Skill4Action;
        _skill5Action = inputBuffer.Skill5Action;
        _skill6Action = inputBuffer.Skill6Action;
        _skillDodge = inputBuffer.DodgeAction;
        _initialized = true;

        if (isActiveAndEnabled)
            Subscribe();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        bool hasAim = TryGetMouseDirection(out Vector3 direction);

        //スキル用の照準方向
        if (hasAim)
            _skillSystem?.UpdateAimDirection(direction);

        // 通常回転にマウス方向を使用するか
        if (!_faceMouseCursor || !hasAim)
        {
            _motor.ClearAimFacingDirection();
            return;
        }

        _motor.SetAimFactingDirection(direction, _characterData.MouseRotationSpeed);
    }

    private void OnEnable()
    {
        if (_initialized)
            Subscribe();

        _skillRequestCts = new CancellationTokenSource();
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;
        if (_skillSystem != null)
        {
            // 通常攻撃と Skill1（連撃）は、再押下・長押し状態を実行中のスキルへ伝える。
            // そのため押下だけでなく解放も購読する。解放の通知自体はスキルを中断しない。
            if (_normalAttack != null)
            {
                _attackAction.performed += OnAttackPerformed;
                _attackAction.canceled += OnAttackCanceled;
            }
            if (_skill1 != null)
            {
                _skill1Action.performed += OnSkill1Performed;
                _skill1Action.canceled += OnSkill1Canceled;
            }
            if (_skill2 != null)
                _skill2Action.started += OnSkill2;
            // Skill3 は Skill2 と同じく単発。押下のみで発動する。
            if (_skill3 != null)
                _skill3Action.started += OnSkill3;
            // Skill4〜6 は長押しで挙動が変わるため、Skill1 と同様に解放も購読する。
            if (_skill4 != null)
            {
                _skill4Action.performed += OnSkill4Performed;
                _skill4Action.canceled += OnSkill4Canceled;
            }
            if (_skill5 != null)
            {
                _skill5Action.performed += OnSkill5Performed;
                _skill5Action.canceled += OnSkill5Canceled;
            }
            if (_skill6 != null)
            {
                _skill6Action.performed += OnSkill6Performed;
                _skill6Action.canceled += OnSkill6Canceled;
            }
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
                {
                    _skill1Action.performed -= OnSkill1Performed;
                    _skill1Action.canceled -= OnSkill1Canceled;
                }
                if (_skill2 != null)
                    _skill2Action.started -= OnSkill2;
                if (_skill3 != null)
                    _skill3Action.started -= OnSkill3;
                if (_skill4 != null)
                {
                    _skill4Action.performed -= OnSkill4Performed;
                    _skill4Action.canceled -= OnSkill4Canceled;
                }
                if (_skill5 != null)
                {
                    _skill5Action.performed -= OnSkill5Performed;
                    _skill5Action.canceled -= OnSkill5Canceled;
                }
                if (_skill6 != null)
                {
                    _skill6Action.performed -= OnSkill6Performed;
                    _skill6Action.canceled -= OnSkill6Canceled;
                }
                if (_skillDodge != null)
                    _skillDodge.started -= OnDodge;
            }
            _subscribed = false;
        }
        // 中断完了を待っている発動要求も取り消し、無効化後に回避が始まるのを防ぐ。
        _skillRequestCts?.Cancel();
        _skillRequestCts?.Dispose();
        _skillRequestCts = null;
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

    private void OnSkill1Performed(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirection)
            ? mouseDirection : transform.forward;

        _skillSystem.HandleSkillInput(_skill1, SkillInputPhase.Pressed, direction);
    }

    private void OnSkill1Canceled(InputAction.CallbackContext context)
    {
        _skillSystem.HandleSkillInput(_skill1, SkillInputPhase.Released, transform.forward);
    }

    private void OnSkill2(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirectoin) ? mouseDirectoin : transform.forward;
        // Skill2 は単発の発動要求として扱い、実行中のスキルへの追加入力は送らない。
        UseSkill(_skill2, direction);
    }

    private void OnSkill3(InputAction.CallbackContext context)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirection) ? mouseDirection : transform.forward;
        // 踏み込み方向は発動時のマウス方向で決まるため、押下時の方向を渡す。
        UseSkill(_skill3, direction);
    }

    private void OnSkill4Performed(InputAction.CallbackContext context) => SendHoldSkillInput(_skill4, SkillInputPhase.Pressed);
    private void OnSkill4Canceled(InputAction.CallbackContext context) => SendHoldSkillInput(_skill4, SkillInputPhase.Released);
    private void OnSkill5Performed(InputAction.CallbackContext context) => SendHoldSkillInput(_skill5, SkillInputPhase.Pressed);
    private void OnSkill5Canceled(InputAction.CallbackContext context) => SendHoldSkillInput(_skill5, SkillInputPhase.Released);
    private void OnSkill6Performed(InputAction.CallbackContext context) => SendHoldSkillInput(_skill6, SkillInputPhase.Pressed);
    private void OnSkill6Canceled(InputAction.CallbackContext context) => SendHoldSkillInput(_skill6, SkillInputPhase.Released);

    /// <summary>
    /// 長押しスキルの押下・解放を SkillSystem へ渡す。
    /// 押下は「未実行なら発動要求／実行中なら押下状態の更新」、解放は実行中のスキルの IsHeld を下ろすだけ。
    /// 解放はスキルを中断しない。長押しをやめた後どうするかは各 Action が IsHeld を見て判断する。
    /// </summary>
    private void SendHoldSkillInput(SkillDefinition definition, SkillInputPhase phase)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirection) ? mouseDirection : transform.forward;
        bool accepted = _skillSystem.HandleSkillInput(definition, phase, direction);

        // 押下だけ記録する。解放は実行していなければ無視されるのが正常なので、ログに出すと紛らわしい。
        if (_logSkillResults && phase == SkillInputPhase.Pressed)
        {
            Debug.Log(
                $"[PlayerController] {definition.SkillName}: " +
                $"accepted={accepted}, cooldown={_skillSystem.GetCooldownRemaing(definition):0.00}s",
                this
            );
        }
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;
        direction.y = 0f;

        // 回避は割り込み要求。実行中の攻撃の後片付けを待ってから開始する。
        // 入力コールバックでは待機せず、非同期処理の例外だけをログへ渡す。
        _skillSystem.TryUseSkillAsync(_dodge, direction.normalized, _skillRequestCts.Token)
            .Forget(ex => Debug.LogException(ex, this));
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
