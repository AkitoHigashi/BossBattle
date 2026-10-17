using UnityEngine;

/// <summary>
/// 攻撃を与えるノード
/// </summary>
[CreateAssetMenu(
    fileName = "AttackNode",
    menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackNode")]
public class AttackNode : ActionNode
{
    [SerializeField, Min(0.01f)]
    private float _attackMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float _attackInterval = 1f;

    private bool _isWaiting;
    private float _finishTime;

    protected override NodeStatus OnUpdate()
    {
        if (!_isWaiting)
        {
            var target = _blackBoard?.Target;
            if (target == null)
                return NodeStatus.Failure;

            var composition =
                target.GetComponent<CharacterComposition>();

            var character = composition?.Character;

            if (character == null ||
                character.Health.CurrentValue <= 0)
            {
                return NodeStatus.Failure;
            }

            int damage = (int)(
                _blackBoard.AttackDamage * _attackMultiplier);

            character.TakeDamage(damage);
            Debug.Log($"{damage}ダメージ", target);

            _finishTime =
                Time.time + Mathf.Max(0f, _attackInterval);

            _isWaiting = true;
        }

        if (Time.time < _finishTime)
            return NodeStatus.Running;

        _isWaiting = false;
        return NodeStatus.Success;
    }
}