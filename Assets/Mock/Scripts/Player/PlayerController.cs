using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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

    [Tooltip("ON: 常にマウス方向を向く / OFF: 歩行中は進行方向、スキル使用中だけマウス方向を向く")]
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
    // 前フレームにスキルを使っていたか。スキルが終わった瞬間を検出するために使う。
    private bool _wasUsingSkill;

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

        // スキルが終わると照準による回転を解除し、CharacterMovement が設定した進行方向の回転に戻る。
        bool isUsingSkill = _skillSystem?.CurrentSkill != null;

        // 今向いている方向を「進行方向」として登録し直す。
        if (_wasUsingSkill && !isUsingSkill && !_faceMouseCursor)
            _motor.SetFacingDirection(transform.forward, _characterData.RotationSpeed);
        _wasUsingSkill = isUsingSkill;

        bool faceMouse = _faceMouseCursor || isUsingSkill;
        if (!faceMouse || !hasAim)
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

    // 連撃の再入力も長押しスキルと同じ経路で渡す。押下ごとに受付結果をログで確認できる。
    private void OnSkill1Performed(InputAction.CallbackContext context) => SendSkillInput(_skill1, SkillInputPhase.Pressed);

    private void OnSkill1Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill1, SkillInputPhase.Released);

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

    private void OnSkill4Performed(InputAction.CallbackContext context) => SendSkillInput(_skill4, SkillInputPhase.Pressed);
    private void OnSkill4Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill4, SkillInputPhase.Released);
    private void OnSkill5Performed(InputAction.CallbackContext context) => SendSkillInput(_skill5, SkillInputPhase.Pressed);
    private void OnSkill5Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill5, SkillInputPhase.Released);
    private void OnSkill6Performed(InputAction.CallbackContext context) => SendSkillInput(_skill6, SkillInputPhase.Pressed);
    private void OnSkill6Canceled(InputAction.CallbackContext context) => SendSkillInput(_skill6, SkillInputPhase.Released);

    /// <summary>
    /// 押下・解放の両方を使うスキル（連撃・長押し・溜め）の入力を SkillSystem へ渡す。
    /// 押下は「未実行なら発動要求／実行中なら押下状態の更新」、解放は実行中のスキルの IsHeld を下ろすだけ。
    /// 解放はスキルを中断しない。長押しをやめた後どうするかは各 Action が IsHeld を見て判断する。
    /// </summary>
    private void SendSkillInput(SkillDefinition definition, SkillInputPhase phase)
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
    /// 設定されているスキルと、その入力キーの一覧を返す（通常攻撃を除く、スキル1〜6と回避の順）。
    /// クールダウン表示などの UI が、スキルとキーの対応をここから受け取る。
    /// </summary>
    /// <remarks>
    /// 対応をここ1か所で管理し、UI 側で同じ設定を持たないようにするためのメソッド。
    /// 通常攻撃はクールダウンが短く、表示してもちらつくだけなので含めない。
    /// </remarks>
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
        // スキル未設定の枠は、押しても何も起きないので表示しない。
        if (skill == null)
            return;

        // キー表示は InputAction の Binding から作る。キーを変更すると表示も自動で追従する。
        string keyLabel = BuildKeyLabel(action, out bool usesShift);
        slots.Add(new SkillSlot(skill, keyLabel, usesShift));
    }

    /// <summary>
    /// Binding のパスから「Shift+RMB」のようなキー表示を作る。Shift 付きの組み合わせかどうかも返す。
    /// </summary>
    /// <remarks>
    /// action.GetBindingDisplayString() は、Binding が現在の操作スキーム（Keyboard&amp;Mouse など）に
    /// 属していないと空文字を返すことがある。このプロジェクトのスキル用 Binding はスキーム未所属のため、
    /// スキームに左右されないよう Binding のパスから直接作っている。
    /// </remarks>
    private static string BuildKeyLabel(InputAction action, out bool usesShift)
    {
        usesShift = false;
        if (action == null)
            return string.Empty;

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            // 「Shift+キー」のような組み合わせ（Composite）は、続く部品を「+」でつなぐ。
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

    /// <summary>
    /// "&lt;Mouse&gt;/rightButton" を "RMB" のような短い表示名に変換する。
    /// </summary>
    private static string ToShortName(string path)
    {
        return InputControlPath.ToHumanReadableString(path,
            InputControlPath.HumanReadableStringOptions.OmitDevice
            | InputControlPath.HumanReadableStringOptions.UseShortNames);
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
