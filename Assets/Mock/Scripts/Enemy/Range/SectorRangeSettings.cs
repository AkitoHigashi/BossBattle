using UnityEngine;

/// <summary>
/// 円・扇形・ドーナツ型の設定、範囲判定、デバッグ描画を持つScriptableObject。
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

    /// <summary>内外の輪郭と、内半径から外側へ広がる塗りを描画する。</summary>
    public void DrawDebug(Vector3 origin, Vector3 forward, float progress)
    {
        forward = SectorRange.GetHorizontalForward(forward);
        DrawSector(origin, forward);
        DrawFilledSector(origin, forward, Mathf.Lerp(_range.InnerRadius, _range.Radius, Mathf.Clamp01(progress)));
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
        float innerRadius = _range.InnerRadius;
        DrawArc(origin, forward, radius, angle);
        if (innerRadius > 0f)
            DrawArc(origin, forward, innerRadius, angle);

        if (angle < FullCircleDegrees)
        {
            Debug.DrawLine(GetArcPoint(origin, forward, innerRadius, -angle * 0.5f),
                GetArcPoint(origin, forward, radius, -angle * 0.5f), Color.yellow);
            Debug.DrawLine(GetArcPoint(origin, forward, innerRadius, angle * 0.5f),
                GetArcPoint(origin, forward, radius, angle * 0.5f), Color.yellow);
        }
    }

    private static void DrawArc(Vector3 origin, Vector3 forward, float radius, float angle)
    {
        int segments = Mathf.Max(1, Mathf.CeilToInt(angle / OutlineStepDegrees));
        Vector3 previous = GetArcPoint(origin, forward, radius, -angle * 0.5f);
        for (int i = 1; i <= segments; i++)
        {
            float yaw = -angle * 0.5f + angle * i / segments;
            Vector3 next = GetArcPoint(origin, forward, radius, yaw);
            Debug.DrawLine(previous, next, Color.yellow);
            previous = next;
        }
    }

    private void DrawFilledSector(Vector3 origin, Vector3 forward, float radius)
    {
        float innerRadius = _range.InnerRadius;
        if (radius <= innerRadius) return;

        float angle = _range.Angle;
        int segments = Mathf.Max(1, Mathf.CeilToInt(angle / FillStepDegrees));
        for (int i = 0; i <= segments; i++)
        {
            float yaw = -angle * 0.5f + angle * i / segments;
            Debug.DrawLine(GetArcPoint(origin, forward, innerRadius, yaw),
                GetArcPoint(origin, forward, radius, yaw), Color.red);
        }
    }

    private static Vector3 GetArcPoint(Vector3 origin, Vector3 forward, float radius, float yaw)
        => origin + Quaternion.AngleAxis(yaw, Vector3.up) * forward * radius;
}
