using UnityEngine;

/// <summary>
/// 円・扇形の設定、範囲判定、のデバッグ描画を持つScriptableObject。
/// </summary>
[CreateAssetMenu(
    fileName = "SectorRangeSettings",
    menuName = "Enemy/Range/Sector")]
public class SectorRangeSettings : ScriptableObject
{
    private const float FullCircleDegrees = 360f;
    private const float OutlineStepDegrees = 5f;
    private const float FillStepDegrees = 4f;

    [SerializeField]
    private SectorRange _range = new SectorRange(5f, 360f);

    public SectorRange Range => _range;

    public bool Contains(
        Vector3 origin,
        Vector3 forward,
        Vector3 targetPosition)
    {
        return _range.Contains(origin, forward, targetPosition);
    }

    /// <summary>最大範囲の外枠と、進行度に応じて中心から広がる塗りを描画する。</summary>
    public void DrawDebug(Vector3 origin, Vector3 forward, float progress)
    {
        forward = SectorRange.GetHorizontalForward(forward);
        DrawSector(origin, forward);
        DrawFilledSector(origin, forward, _range.Radius * Mathf.Clamp01(progress));
    }

    /// <summary>
    /// 指定した原点と前方方向を基準に、範囲角と半径で扇形の輪郭をデバッグ描画する。
    /// </summary>
    /// <param name="origin">扇形の中心位置。</param>
    /// <param name="forward">扇形の基準前方方向。</param>
    private void DrawSector(Vector3 origin, Vector3 forward)
    {
        float angle = _range.Angle;
        float radius = _range.Radius;
        int segments = Mathf.Max(1, Mathf.CeilToInt(angle / OutlineStepDegrees));

        Vector3 previous = GetArcPoint(origin, forward, radius, -angle * 0.5f);
        if (angle < FullCircleDegrees)
            Debug.DrawLine(origin, previous, Color.yellow);

        for (int i = 1; i <= segments; i++)
        {
            float yaw = -angle * 0.5f + angle * i / segments;
            Vector3 next = GetArcPoint(origin, forward, radius, yaw);
            Debug.DrawLine(previous, next, Color.yellow);
            previous = next;
        }

        if (angle < FullCircleDegrees)
            Debug.DrawLine(origin, previous, Color.yellow);
    }

    private void DrawFilledSector(Vector3 origin, Vector3 forward, float radius)
    {
        if (radius <= 0f) return;

        float angle = _range.Angle;
        int segments = Mathf.Max(1, Mathf.CeilToInt(angle / FillStepDegrees));
        for (int i = 0; i <= segments; i++)
        {
            float yaw = -angle * 0.5f + angle * i / segments;
            Debug.DrawLine(origin, GetArcPoint(origin, forward, radius, yaw), Color.red);
        }
    }

    private static Vector3 GetArcPoint(Vector3 origin, Vector3 forward, float radius, float yaw)
        => origin + Quaternion.AngleAxis(yaw, Vector3.up) * forward * radius;
}
