using UnityEngine;

public readonly struct HitInfo
{
    public HitInfo(HitInfo hitInfo)
    {
        Target = hitInfo.Target;
        HitPoint = hitInfo.HitPoint;
        HitDirection = hitInfo.HitDirection;
        HitCollider = hitInfo.HitCollider;
    }

    public HitInfo(Character character)
        : this(character, Vector3.zero, Vector3.forward, null)
    {
    }

    public HitInfo(Character character, Vector3 hitPoint, Vector3 hitDirection, Collider hitCollider)
    {
        Target = character;
        HitPoint = hitPoint;
        HitDirection = hitDirection.sqrMagnitude > 0f ? hitDirection.normalized : Vector3.forward;
        HitCollider = hitCollider;
    }


    public Character Target { get; }
    public Vector3 HitPoint { get; }
    public Vector3 HitDirection { get; }
    public Collider HitCollider { get; }
}
