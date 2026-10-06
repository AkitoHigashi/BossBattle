using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "BossBattle/Character Data")]
public class CharacterData : ScriptableObject
{
    [SerializeField, Min(0f)] private float _moveSpeed;

    public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
}
