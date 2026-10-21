using UnityEngine;

/// <summary>予測表示と命中判定で共有する、確定済みの攻撃位置。</summary>
public readonly struct AttackPose
{
    public AttackPose(Vector3 origin, Vector3 forward)
    {
        Origin = origin;
        Forward = SectorRange.GetHorizontalForward(forward);
    }

    public Vector3 Origin { get; }
    public Vector3 Forward { get; }
}
