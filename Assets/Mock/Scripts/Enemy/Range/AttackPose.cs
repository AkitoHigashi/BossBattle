using UnityEngine;
using UnityEngine.AI;

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

    public static bool TryAtGround(Transform owner, NavMeshAgent agent, float searchRadius, float yawOffset, out AttackPose pose)
    {
        pose = default;
        if (owner == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 origin = owner.position;
        Vector3 ground = origin - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, searchRadius), filter))
            return false;

        origin.y = hit.position.y;
        Vector3 forward = SectorRange.GetHorizontalForward(owner.forward);
        forward = Quaternion.AngleAxis(yawOffset, Vector3.up) * forward;
        pose = new AttackPose(origin, forward);
        return true;
    }
}
