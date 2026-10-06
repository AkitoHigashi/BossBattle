using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "BossBattle/Character Data")]
public class CharacterData : ScriptableObject
{
    public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
    public int MaxHealth => Mathf.Max(1, _maxHealth);

    [SerializeField, Min(0f)] private float _moveSpeed;
    [SerializeField,Min(1)] private int _maxHealth = 1;

}
