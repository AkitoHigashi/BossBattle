using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "BossBattle/Character Data")]
public class CharacterData : ScriptableObject
{
    public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
    public float RotationSpeed => Mathf.Max(0f, _rotationSpeed);
    public float MouseRotationSpeed => Mathf.Max(0f, _mouseRotationSpeed);
    public int MaxHealth => Mathf.Max(1, _maxHealth);

    [SerializeField, Min(0f)] private float _moveSpeed;
    [SerializeField, Min(0f), Tooltip("WASD移動方向へ向く旋回速度（度/秒）。0で旋回を停止します。")]
    private float _rotationSpeed = 720f;
    [SerializeField, Min(0f), Tooltip("マウスカーソル方向への旋回速度（度/秒）")]
    private float _mouseRotationSpeed = 1440f;
    [SerializeField, Min(1)] private int _maxHealth = 1;

}
