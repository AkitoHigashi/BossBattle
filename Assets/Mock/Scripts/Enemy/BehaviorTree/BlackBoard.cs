using R3;
using UnityEngine.AI;

public class BlackBoard
{
    public BlackBoard(ReactiveProperty<float> health, float attackRange, NavMeshAgent navMeshAgent)
    {
        _health = health;
        _attackRange = attackRange;
        _navMeshAgent = navMeshAgent;
    }

    public ReactiveProperty<float> Health => _health;
    public float AttackRange => _attackRange;
    public NavMeshAgent NavMeshAgent => _navMeshAgent;

    private ReactiveProperty<float> _health;
    private float _attackRange;
    private NavMeshAgent _navMeshAgent;
}
