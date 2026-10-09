using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 攻撃の定義
/// </summary>
[CreateAssetMenu(fileName = "AttackDefinition", menuName = "BossBattle/AttackDefinition", order = 1)]
public class AttackDefinition : ScriptableObject
{
    [SerializeField]
    private List<HitEffect> _hitEffects = new List<HitEffect>();
    [SerializeField]
    private AttackData _attackData;
}
