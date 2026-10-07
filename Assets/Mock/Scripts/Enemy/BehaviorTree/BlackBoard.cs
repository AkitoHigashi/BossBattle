using R3;
using UnityEngine.AI;

public class BlackBoard
{
    public BlackBoard(PlayerMovement target, ReactiveProperty<float> health, float attackRange, NavMeshAgent navMeshAgent)
    {
        _target = target;
        _health = health;
        _attackRange = attackRange;
        _navMeshAgent = navMeshAgent;
    }
    public PlayerMovement Target => _target;
    public ReactiveProperty<float> Health => _health;
    public float AttackRange => _attackRange;
    public NavMeshAgent NavMeshAgent => _navMeshAgent;

    private PlayerMovement _target;
    private ReactiveProperty<float> _health;
    private float _attackRange;
    private NavMeshAgent _navMeshAgent;
}
