using UnityEngine;

/// <summary>
///  攻撃システム時の初期データを格納する構造体
/// </summary>
[CreateAssetMenu(fileName = "AttackData", menuName = "BossBattle/AttackData", order = 1)]

public class AttackData : ScriptableObject
{
    public int BaseDamage => _baseDamage;
    [SerializeField]
    private int _baseDamage;    
}
