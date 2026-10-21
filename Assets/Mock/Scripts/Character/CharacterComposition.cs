using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
    public CharacterData CharacterData => _characterData;
    public Character Character { get; private set; }
    public SkillSystem SkillSystem { get; private set; }

    [Header("Character")]
    [SerializeField] private CharacterData _characterData;

    [Header("Skill Dependencies")]
    [SerializeField] private Animator _animator;
    [SerializeField] private AttackHitbox _attackHitbox;
    [SerializeField] private AttackAnimationEvents _animationEvents;


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
            SkillSystem = new SkillSystem(context);
        }
        Character = new Character(health, status, movement);
    }

    private void OnDisable() => SkillSystem?.CancelActiveSkill();
    private void OnDestroy()
    {
        SkillSystem?.Dispose();
    }
}
