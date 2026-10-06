using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
    [SerializeField] private CharacterData _characterData;
    public Character Character { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (Character != null)
            return;

        if (_characterData == null)
            throw new System.InvalidOperationException("キャラクターのデータが設定されていません。");

        var movement = new CharacterMovement(_characterData, GetComponent<CharacterMotor>());
        var health = new HealthEntity(_characterData.MaxHealth);
        var status = new Status(0, 0, 0);
        Character = new Character(health, status, movement);
    }
}
