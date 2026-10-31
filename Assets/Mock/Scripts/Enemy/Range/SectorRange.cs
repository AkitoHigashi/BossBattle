using System;
using UnityEngine;

/// <summary>
/// 外半径・内半径・角度で、円・扇形・ドーナツ型の範囲を表す。
/// </summary>
[Serializable]
public struct SectorRange
{
    private const float FullCircleDegrees = 360f;
    private const float MinRadius = 0.01f;
    private const float DirectionSqrTolerance = 0.000001f;

    [SerializeField, Min(MinRadius)]
    private float _radius;

    [SerializeField, Min(0f), Tooltip("内側の安全地帯")]
    private float _innerRadius;

    [SerializeField, Range(0f, FullCircleDegrees)]
    private float _angle;

    public float Radius => Mathf.Max(MinRadius, _radius);

    public float InnerRadius => Mathf.Clamp(_innerRadius, 0f, Radius);

    public float Angle =>
        Mathf.Clamp(_angle, 0f, FullCircleDegrees);

    public SectorRange(float radius, float angle, float innerRadius = 0f)
    {
        _radius = radius;
        _angle = angle;
        _innerRadius = innerRadius;
    }

    /// <summary>
    /// 指定した位置が扇形の範囲内に含まれるかどうかを判定する
    /// </summary>
    /// <param name="center">扇形の中心点</param>
    /// <param name="forward">扇形の前方向</param>
    /// <param name="targetPosition">判定する位置</param>
    /// <returns></returns>
    public bool Contains(
        Vector3 center,
        Vector3 forward,
        Vector3 targetPosition)
    {
        Vector3 offset = targetPosition - center;
        offset.y = 0f;

        float radius = Radius;

        float innerRadius = InnerRadius;
        if (offset.sqrMagnitude > radius * radius || offset.sqrMagnitude < innerRadius * innerRadius)
            return false;

        float angle = Angle;

        if (angle >= FullCircleDegrees)
            return true;

        forward = GetHorizontalForward(forward);

        return Vector3.Angle(forward, offset)
            <= angle * 0.5f;
    }

    public static Vector3 GetHorizontalForward(Vector3 forward)
    {
        forward = Vector3.ProjectOnPlane(
            forward, Vector3.up).normalized;

        return forward.sqrMagnitude < DirectionSqrTolerance
            ? Vector3.forward
            : forward;
    }
}
