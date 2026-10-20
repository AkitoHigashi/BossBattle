using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 上に上がるよ
/// </summary>
[CreateAssetMenu(
    fileName = "MoveUpNode",
    menuName = "Behavior Tree/Mock/Nodes/Action Nodes/MoveUpNode")]
public sealed class MoveUpNode : MoveVerticalNode
{
    protected override float Direction => 1f;

    protected override bool TryBeginMovement(NavMeshAgent agent)
    {
        if (_blackBoard.Target == null)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 ground = agent.transform.position - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, 2f, filter))
            return false;

        // 上昇を始めるときに戻り先を保存する
        var attack = _blackBoard.AerialAttack;
        attack.LandingBaseOffset = agent.baseOffset;
        attack.GroundPosition = hit.position;
        attack.HasLandingPoint = true;
        attack.HasPrediction = false;
        attack.DamageApplied = false;
        _blackBoard.PredictionView?.Hide();
        return true;
    }
}
