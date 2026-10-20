using UnityEngine;

/// <summary>敵ごとの落下攻撃データ</summary>
public sealed class AerialAttackContext
{
    public bool HasLandingPoint { get; set; }
    public float LandingBaseOffset { get; set; }
    public Vector3 GroundPosition { get; set; }
    public bool HasPrediction { get; set; }
    public AttackPose Pose { get; set; }
    public SectorRange Range { get; set; }
    public bool DamageApplied { get; set; }
}
