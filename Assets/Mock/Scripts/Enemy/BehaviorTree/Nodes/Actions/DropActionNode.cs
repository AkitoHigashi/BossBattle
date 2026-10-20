using UnityEngine;

/// <summary>下降し、着地時に範囲攻撃を行って予測表示を消す。</summary>
[CreateAssetMenu(fileName = "DropAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/DropAttackNode")]
public sealed class DropActionNode : AttackNode
{
    [SerializeField, Min(0.01f)] private float _fallSpeed = 24f;

    protected override NodeStatus OnUpdate()
    {
        var attack = _blackBoard?.AerialAttack;
        var agent = _blackBoard?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh ||
            !attack.HasLandingPoint || !attack.HasPrediction)
            return NodeStatus.Failure;

        // 同じ攻撃の再評価でもダメージは一度だけ。
        if (attack.DamageApplied)
            return NodeStatus.Success;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.baseOffset = Mathf.MoveTowards(agent.baseOffset, attack.LandingBaseOffset,
            Mathf.Max(0.01f, _fallSpeed) * Time.deltaTime);

        if (!Mathf.Approximately(agent.baseOffset, attack.LandingBaseOffset))
            return NodeStatus.Running;

        return ApplyDamage();
    }

    private NodeStatus ApplyDamage()
    {
        var attack = _blackBoard.AerialAttack;
        attack.DamageApplied = true;
        var target = _blackBoard.Target;
        if (target == null || !attack.Range.Contains(attack.Pose.Origin, attack.Pose.Forward, target.transform.position))
            return NodeStatus.Success;

        var character = target.GetComponent<CharacterComposition>()?.Character;
        if (character == null)
        {
            Debug.LogWarning("範囲攻撃の対象にCharacterCompositionがありません。", target);
            return NodeStatus.Failure;
        }

        if (character.Health.CurrentValue > 0)
        {
            int damage = Mathf.RoundToInt(Damage);
            character.TakeDamage(damage);
            Debug.Log($"落下攻撃：{damage}ダメージ", target);
        }

        return NodeStatus.Success;
    }

    protected override void OnSuccess() => HidePrediction();
    protected override void OnFailure() => HidePrediction();

    private void HidePrediction()
    {
        _blackBoard?.PredictionView?.Hide();
    }
}
