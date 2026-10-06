using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
[DisallowMultipleComponent]
public class CharacterComposition : MonoBehaviour
{
    [SerializeField] private CharacterData _characterData;
    [SerializeField, Min(1)] private int _maxHealth = 1;

    private Character _character;

    public Character Character
    {
        get
        {
            if (_character == null)
                Initialize();
            return _character;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_character != null)
            return;

        if (_characterData == null)
            throw new System.InvalidOperationException("キャラクターのデータが設定されていません。");

        var movement = new CharacterMovement(_characterData, GetComponent<CharacterMotor>());
        var health = new HealthEntity(Mathf.Max(1, _maxHealth));
        var status = new Status(0, 0, 0);
        _character = new Character(health, status, movement);
    }
}
