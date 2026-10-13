using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
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
        Character = new Character(health, status, movement);
        _attackHitbox.Init(transform);
        SkillContext context = new SkillContext(motor,_animator,_animationEvents);
        SkillSystem = new SkillSystem(context);
    }
    private void OnDestroy()
    {
        SkillSystem?.Dispose();
    }
}
