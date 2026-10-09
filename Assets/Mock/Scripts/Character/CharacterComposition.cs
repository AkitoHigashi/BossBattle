using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(MotionSystem))]
[RequireComponent(typeof(AttackResolver))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
    [SerializeField] private CharacterData _characterData;
    [SerializeField] private MotionSystem _motionSystem;

    public Character Character { get; private set; }

    public void Initialize()
    {
        if (Character != null)
            return;

        if (_characterData == null)
            throw new System.InvalidOperationException("Character data is not assigned.");

        var motor = GetComponent<CharacterMotor>();
        var movement = new CharacterMovement(_characterData, motor);
        var health = new HealthEntity(_characterData.MaxHealth);
        var status = new Status(0, 0, 0);

        if (_motionSystem == null)
            _motionSystem = GetComponent<MotionSystem>();
        if (_motionSystem != null)
            _motionSystem.Initialize();
        PlayerAttackRunner attackRunner = new PlayerAttackRunner(Character, _motionSystem, this);
        
        Character = new Character(health, status, movement, attackRunner, motor);
    }
}
