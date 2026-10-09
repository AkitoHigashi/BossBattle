using UnityEngine;

[CreateAssetMenu(fileName = "AttackDefinition", menuName = "BossBattle/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public int BaseDamage => Mathf.Max(0, _baseDamage);
    public MotionData MotionData => _motionData;
    public DamageCalculation DamageCalculation => _damageCalculation;
    public HitEffect[] HitEffects => _hitEffects;
    public bool AllowMultipleHitsPerActivation => _allowMultipleHitsPerActivation;

    [SerializeField, Min(0)] private int _baseDamage = 1;
    [SerializeField] private MotionData _motionData;
    [SerializeField] private DamageCalculation _damageCalculation;
    [SerializeField] private HitEffect[] _hitEffects = new HitEffect[0];
    [SerializeField] private bool _allowMultipleHitsPerActivation;
}
