using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーの入力を、移動とスキルの要求に変換して渡す。
/// </summary>
/// <remarks>
/// 読む順番：
/// 1. Initialize / Subscribe … 入力イベントの購読
/// 2. Update … 毎フレームの向き（マウス方向か進行方向か）
/// 3. 移動入力 … OnMove（斜め移動の離し方の補正つき）
/// 4. スキル入力 … OnSkill◯ から SkillSystem へ渡す
///
/// スキルの中身や実行管理は持たない。「どのスキルを・どの方向へ・押したか離したか」を SkillSystem に渡すだけ。
/// </remarks>
[RequireComponent(typeof(CharacterMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Skill")]
    [Tooltip("通常攻撃（左クリック）")]
    [SerializeField] private SkillDefinition _normalAttack;
    [Tooltip("3連撃（右クリック）")]
    [SerializeField] private SkillDefinition _skill1;
    [Tooltip("ブリンク斬り（Q）")]
    [SerializeField] private SkillDefinition _skill2;
    [Tooltip("踏み込み斬りつけ（E）")]
    [SerializeField] private SkillDefinition _skill3;
    [Tooltip("前方連続突き（Shift+右クリック長押し）")]
    [SerializeField] private SkillDefinition _skill4;
    [Tooltip("溜め3連（Shift+Q長押し）")]
    [SerializeField] private SkillDefinition _skill5;
    [Tooltip("溜め斬りつけ（Shift+E長押し）")]
    [SerializeField] private SkillDefinition _skill6;
    [Tooltip("回避（Space）。実行中のスキルに割り込める")]
    [SerializeField] private SkillDefinition _dodge;

    [Header("向き・移動")]
    [Tooltip("ON: 常にマウス方向を向く / OFF: 歩行中は進行方向、スキル中だけマウス方向")]
    [SerializeField] private bool _faceMouseCursor = true;
    [Tooltip("斜めからキーを1つ離したとき、縦横に切り替えるまで待つ秒数")]
    [SerializeField, Min(0f)] private float _diagonalReleaseGrace = 0.1f;
    [Tooltip("マウス方向の計算に使うカメラ。未設定なら Camera.main")]
    [SerializeField] private Camera _camera;

    [Header("デバッグ")]
    [Tooltip("スキルの受付結果を Console に出す")]
    [SerializeField] private bool _logSkillResults = false;

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
    // 回避の割り込み要求（非同期）を、無効化時にまとめて取り消すためのもの。
    private CancellationTokenSource _skillRequestCts;
    private bool _initialized;
    private bool _subscribed;
    private bool _wasUsingSkill;
    private Vector2 _appliedMoveInput;
    private Vector2 _pendingMoveInput;
    private float _pendingMoveSince;
    private bool _hasPendingMove;

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
            // 連撃・長押し・溜めのスキルは、押下（performed）と解放（canceled）の両方を購読する。
            // 単発のスキル（Skill2・3・回避）は押下（started）だけ。
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
            if (_skill3 != null)
                _skill3Action.started += OnSkill3;
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

        ApplyMove(_moveAction.ReadValue<Vector2>());
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
        // 後片付け待ちの回避要求も取り消し、無効化後に回避が始まらないようにする。
        _skillRequestCts?.Cancel();
        _skillRequestCts?.Dispose();
        _skillRequestCts = null;
        _motor?.ClearAimFacingDirection();
        if (_character != null)
            ApplyMove(Vector2.zero);
        _skillSystem?.CancelActiveSkill();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (_hasPendingMove && Time.time - _pendingMoveSince >= _diagonalReleaseGrace)
            ApplyMove(_pendingMoveInput);

        bool hasAim = TryGetMouseDirection(out Vector3 direction);
        if (hasAim)
            _skillSystem?.UpdateAimDirection(direction);

        // 連撃の入力待ちの間は歩けるので、向きも通常の移動と同じ扱いにする（マウス方向に固定しない）。
        SkillDefinition currentSkill = _skillSystem?.CurrentSkill;
        bool isUsingSkill = currentSkill != null
            && !_skillSystem.TryGetInputWindow(currentSkill, out _, out _);

        if (_wasUsingSkill && !isUsingSkill && !_faceMouseCursor)
            RestoreFacingAfterSkill();
        _wasUsingSkill = isUsingSkill;

        bool faceMouse = _faceMouseCursor || isUsingSkill;
        if (!faceMouse || !hasAim)
        {
            _motor.ClearAimFacingDirection();
            return;
        }

        _motor.SetAimFactingDirection(direction, _characterData.MouseRotationSpeed);
    }

    /// <summary>
    /// スキル終了時（OFF の設定）の向きを決める。キーを押していればその方向、押していなければ今の向きのまま。
    /// </summary>
    private void RestoreFacingAfterSkill()
    {
        if (_appliedMoveInput.sqrMagnitude > 0.0001f)
            _character.Move(new MovementData(new Vector3(_appliedMoveInput.x, 0f, _appliedMoveInput.y)));
        else
            _motor.SetFacingDirection(transform.forward, _characterData.RotationSpeed);
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();

        // 斜め（W+D）の2キーを同時に離すのは難しく、一瞬だけ片方（D）の入力が入る。
        // そのまま反映すると真横を向いて止まるため、斜め→縦横の変化は猶予時間だけ反映を待つ。
        if (_diagonalReleaseGrace > 0f && IsPartialDiagonalRelease(_appliedMoveInput, input))
        {
            if (!_hasPendingMove)
                _pendingMoveSince = Time.time;
            _pendingMoveInput = input;
            _hasPendingMove = true;
            return;
        }

        ApplyMove(input);
    }

    private void ApplyMove(Vector2 input)
    {
        _hasPendingMove = false;
        _appliedMoveInput = input;
        _character.Move(new MovementData(new Vector3(input.x, 0f, input.y)));
    }

    /// <summary>
    /// 斜めからキーを1つ離して、同じ向きの縦か横だけになったか（例：W+D → D）。
    /// 逆方向への切り替え（W+D → A）は意図的な操作なので対象外。
    /// </summary>
    private static bool IsPartialDiagonalRelease(Vector2 previous, Vector2 next)
    {
        const float epsilon = 0.01f;
        bool wasDiagonal = Mathf.Abs(previous.x) > epsilon && Mathf.Abs(previous.y) > epsilon;
        if (!wasDiagonal)
            return false;

        bool onlyX = Mathf.Abs(next.x) > epsilon && Mathf.Abs(next.y) <= epsilon;
        bool onlyY = Mathf.Abs(next.y) > epsilon && Mathf.Abs(next.x) <= epsilon;
        return (onlyX && Mathf.Sign(next.x) == Mathf.Sign(previous.x))
            || (onlyY && Mathf.Sign(next.y) == Mathf.Sign(previous.y));
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

    // RMB・Q・E には Shift 付きの別スキル（4〜6）があるため、Shift 押下中は Shift なしのスキル（1〜3）を発動しない。
    // 解放（Canceled）は止めない。止めると実行中のスキルが「押したまま」と記録され続けてしまう。
    private void OnSkill1Performed(InputAction.CallbackContext context)
    {
        if (IsShiftHeld())
            return;
        SendSkillInput(_skill1, SkillInputPhase.Pressed);
    }
    private void OnSkill1Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill1, SkillInputPhase.Released);

    private void OnSkill2(InputAction.CallbackContext context)
    {
        if (IsShiftHeld())
            return;
        Vector3 direction = TryGetMouseDirection(out var mouseDirectoin) ? mouseDirectoin : transform.forward;
        UseSkill(_skill2, direction);
    }

    private void OnSkill3(InputAction.CallbackContext context)
    {
        if (IsShiftHeld())
            return;
        Vector3 direction = TryGetMouseDirection(out var mouseDirection) ? mouseDirection : transform.forward;
        UseSkill(_skill3, direction);
    }

    private void OnSkill4Performed(InputAction.CallbackContext context) => SendSkillInput(_skill4, SkillInputPhase.Pressed);
    private void OnSkill4Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill4, SkillInputPhase.Released);
    private void OnSkill5Performed(InputAction.CallbackContext context) => SendSkillInput(_skill5, SkillInputPhase.Pressed);
    private void OnSkill5Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill5, SkillInputPhase.Released);
    private void OnSkill6Performed(InputAction.CallbackContext context) => SendSkillInput(_skill6, SkillInputPhase.Pressed);
    private void OnSkill6Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill6, SkillInputPhase.Released);

    /// <summary>
    /// 連撃・長押し・溜めのスキルへ、押下か解放を渡す。
    /// 押下は「未実行なら発動、実行中なら押下として記録」。解放はスキルを止めず、離したことを記録するだけ。
    /// </summary>
    private void SendSkillInput(SkillDefinition definition, SkillInputPhase phase)
    {
        Vector3 direction = TryGetMouseDirection(out var mouseDirection) ? mouseDirection : transform.forward;
        if (phase == SkillInputPhase.Pressed && TryInterruptWith(definition, direction))
            return;
        bool accepted = _skillSystem.HandleSkillInput(definition, phase, direction);

        // 解放は未実行なら無視されるのが正常なので、ログは押下だけにする。
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

        // 回避は割り込み。実行中の攻撃の後片付けを待ってから始まるので、ここでは待たずに投げるだけ。
        _skillSystem.TryUseSkillAsync(_dodge, direction.normalized, _skillRequestCts.Token)
            .Forget(ex => Debug.LogException(ex, this));
    }

    /// <summary>
    /// 別のスキルを実行中なら、割り込みでの発動を要求して true を返す（回避と同じ経路）。
    /// 割り込めるかは各スキルの Can Interrupt Others / Can Be Interrupted の設定で決まる。
    /// </summary>
    /// <remarks>
    /// TryUseSkill / HandleSkillInput は「実行中なら発動しない」ので、
    /// これを通さないと連撃の入力待ち中などに Q・E が効かなくなる。
    /// </remarks>
    private bool TryInterruptWith(SkillDefinition definition, Vector3 direction)
    {
        SkillDefinition current = _skillSystem.CurrentSkill;
        if (current == null || current == definition)
            return false;

        _skillSystem.TryUseSkillAsync(definition, direction, _skillRequestCts.Token)
            .Forget(ex => Debug.LogException(ex, this));
        return true;
    }

    private void UseSkill(SkillDefinition definition, Vector3 direction)
    {
        if (_skillSystem == null || definition == null)
            return;
        if (TryInterruptWith(definition, direction))
            return;

        bool accepted = _skillSystem.TryUseSkill(definition, direction);

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

    // ───────────── UI 向けの情報 ─────────────

    /// <summary>
    /// スキルとキーの一覧を返す（スキル1〜6と回避。通常攻撃は短すぎて表示がちらつくので除く）。
    /// スキルとキーの対応はここだけで管理し、UI はこれを受け取って表示する。
    /// </summary>
    public SkillSlot[] GetSkillSlots()
    {
        if (!_initialized)
            throw new InvalidOperationException("PlayerControllerの初期化後に呼び出してください。");

        var slots = new List<SkillSlot>();
        AddSlot(slots, _skill1, _skill1Action);
        AddSlot(slots, _skill2, _skill2Action);
        AddSlot(slots, _skill3, _skill3Action);
        AddSlot(slots, _skill4, _skill4Action);
        AddSlot(slots, _skill5, _skill5Action);
        AddSlot(slots, _skill6, _skill6Action);
        AddSlot(slots, _dodge, _skillDodge);
        return slots.ToArray();
    }

    private static void AddSlot(List<SkillSlot> slots, SkillDefinition skill, InputAction action)
    {
        if (skill == null)
            return;

        string keyLabel = BuildKeyLabel(action, out bool usesShift);
        slots.Add(new SkillSlot(skill, keyLabel, usesShift));
    }

    /// <summary>
    /// Binding から「Shift+RMB」のようなキー表示を作る。Shift 付きかどうかも返す。
    /// </summary>
    private static string BuildKeyLabel(InputAction action, out bool usesShift)
    {
        usesShift = false;
        if (action == null)
            return string.Empty;

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            // 組み合わせ（Composite）は、後に続く部品を「+」でつなぐ。
            if (binding.isComposite)
            {
                var parts = new List<string>();
                for (int part = i + 1; part < bindings.Count && bindings[part].isPartOfComposite; part++)
                {
                    string path = bindings[part].effectivePath;
                    if (bindings[part].name == "modifier" && path.Contains("shift"))
                        usesShift = true;
                    parts.Add(ToShortName(path));
                }
                return string.Join("+", parts);
            }

            if (!binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath))
                return ToShortName(binding.effectivePath);
        }

        return string.Empty;
    }

    // "<Mouse>/rightButton" → "RMB"
    private static string ToShortName(string path)
    {
        return InputControlPath.ToHumanReadableString(path,
            InputControlPath.HumanReadableStringOptions.OmitDevice
            | InputControlPath.HumanReadableStringOptions.UseShortNames);
    }

    // ───────────── 補助 ─────────────

    /// <summary>
    /// Shift を押しているか。Shift なし／ありのスキルの振り分けに使う。
    /// なぜか一部の人がQスキルが優先などのバグが起きるため。
    /// </summary>
    private static bool IsShiftHeld()
    {
        return Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
    }

    /// <summary>
    /// マウスカーソルが指す地面上の点への、水平な方向を求める。
    /// </summary>
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
