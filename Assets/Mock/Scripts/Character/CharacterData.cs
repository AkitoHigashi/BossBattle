using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "BossBattle/Character Data")]
public class CharacterData : ScriptableObject
{
    public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
    public float RotationSpeed => Mathf.Max(0f, _rotationSpeed);
    public int MaxHealth => Mathf.Max(1, _maxHealth);

    [SerializeField, Min(0f)] private float _moveSpeed;
    [SerializeField, Min(0f), Tooltip("移動方向へ向く旋回速度（度/秒）。0で旋回を停止します。")]
    private float _rotationSpeed = 720f;
    [SerializeField,Min(1)] private int _maxHealth = 1;

}
