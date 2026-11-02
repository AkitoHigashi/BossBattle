/// <summary>範囲攻撃データ</summary>
public sealed class AttackContext
{
    /// <summary>予測表示に成功し、位置・範囲が確定したかどうか</summary>
    public bool HasPrediction { get; set; }
    public AttackPose Pose { get; set; }
    public SectorRange Range { get; set; }

    /// <summary>攻撃判定を正常に処理済みかどうか</summary>
    public bool DamageApplied { get; set; }
}
