using UnityEngine;

public interface IRangeAttackNode
{
    bool IsInAttackRange(
        Vector3 origin,
        Vector3 forward,
        Vector3 targetPosition);
}
