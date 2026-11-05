using UnityEngine;

/// <summary>予兆の後に下降し、着地時に攻撃する。</summary>
public sealed class DropAttackNode : AttackNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Time To Attackの間、足元の地面に予兆を出した後、地面の高さまで落下し、着地した瞬間にダメージを与える。\nWarning Area未設定なら予兆なしでターゲットに必ず当たる。Recovery Timeが過ぎたらSuccess。";

    [SerializeField, Min(0.01f), Tooltip("落下する速さ（m/秒）")] private float _fallSpeed = 24f;

    protected override NodeStatus OnAttack()
    {
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return NodeStatus.Failure;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.baseOffset = Mathf.MoveTowards(agent.baseOffset, _blackBoard.GroundBaseOffset,
            Mathf.Max(0.01f, _fallSpeed) * Time.deltaTime);

        // 落下中も予兆は出したまま
        if (!Mathf.Approximately(agent.baseOffset, _blackBoard.GroundBaseOffset))
            return NodeStatus.Running;

        // 着地の瞬間に判定する
        Hit();
        return NodeStatus.Success;
    }
}
