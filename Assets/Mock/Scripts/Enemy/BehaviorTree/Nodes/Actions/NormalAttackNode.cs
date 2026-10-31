using UnityEngine;

/// <summary>対象にダメージを与え、攻撃後の待ち時間が終わるまでRunningを返す。</summary>
[CreateAssetMenu(fileName = "NormalAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/NormalAttackNode")]
public sealed class NormalAttackNode : AttackNode
{
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

            var character = target.GetComponent<CharacterComposition>()?.Character;
            if (character == null || character.Health.CurrentValue <= 0)
                return NodeStatus.Failure;

            int damage = (int)Damage;
            character.TakeDamage(damage);
            Debug.Log($"{damage}ダメージ", target);

            _finishTime = Time.time + Mathf.Max(0f, _attackInterval);
            _isWaiting = true;
        }

        if (Time.time < _finishTime)
            return NodeStatus.Running;

        _isWaiting = false;
        return NodeStatus.Success;
    }

    // 攻撃後の待ち時間の途中で中断されたら、次は攻撃からやり直す
    protected override void OnAbort() => _isWaiting = false;
}
