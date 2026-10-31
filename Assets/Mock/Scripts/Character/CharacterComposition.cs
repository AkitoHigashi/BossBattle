using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
    public CharacterData CharacterData => _characterData;
    public Character Character { get; private set; }
    public SkillSystem SkillSystem { get; private set; }
    // 破棄時に片付けるため保持する（飛び道具のプールなどを持つ）。
    private SkillContext _skillContext;

    [Header("Character")]
    [SerializeField] private CharacterData _characterData;

    [Header("Skill Dependencies")]
    [SerializeField] private Animator _animator;
    [SerializeField] private AttackHitbox _attackHitbox;
    [SerializeField] private AttackAnimationEvents _animationEvents;

    [Header("UI（任意）")]
    [Tooltip("溜めゲージの表示。未設定でも溜めスキルは動作し、ゲージが出ないだけ")]
    [SerializeField] private ChargeGaugeView _chargeGaugeView;
    [Tooltip("全スキルのクールダウン表示。未設定でもスキルは動作し、表示が出ないだけ")]
    [SerializeField] private SkillCooldownView _cooldownView;


    public void Initialize()
    {
        if (Character != null)
            return;

        if (_characterData == null)
            throw new System.InvalidOperationException("キャラクターのデータが設定されていません。");

        CharacterMotor motor = GetComponent<CharacterMotor>();
        var movement = new CharacterMovement(_characterData, motor);
        var health = new HealthEntity(_characterData.MaxHealth);
        var status = new Status(0, 0, 0);
        bool hasCombat = _animator != null || _attackHitbox != null || _animationEvents != null;
        if (hasCombat)
        {
            if (_animator == null || _attackHitbox == null || _animationEvents == null)
                throw new System.InvalidOperationException("スキルにはAnimator、AttackHitbox、AttackAnimationEventsの全参照が必要です。");
            _attackHitbox.Init(transform);
            _animationEvents.Init(_attackHitbox);
            SkillContext context = new SkillContext(motor, _animator, _animationEvents);
            _skillContext = context;
            SkillSystem = new SkillSystem(context);
            // ゲージのデータは SkillContext がキャラクターごとに持つので、View にはそれを渡す。
            if (_chargeGaugeView != null)
                _chargeGaugeView.Initialize(context.ChargeGauge, transform);
            // クールダウンは SkillSystem が管理しているので、表示にはそれを渡して読ませる。
            if (_cooldownView != null)
                _cooldownView.Initialize(SkillSystem);
        }
        Character = new Character(health, status, movement);
    }

    private void OnDisable() => SkillSystem?.CancelActiveSkill();
    private void OnDestroy()
    {
        // 先にスキルを止めてから、スキルが使っていたリソースを片付ける。
        SkillSystem?.Dispose();
        _skillContext?.Dispose();
    }
}
