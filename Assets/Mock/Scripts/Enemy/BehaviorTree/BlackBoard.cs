using R3;
using UnityEngine;
using UnityEngine.AI;

public class BlackBoard
{
    public BlackBoard(PlayerMovement target,
        ReactiveProperty<float> health, 
        float attackRange, 
        NavMeshAgent navMeshAgent,
        Animator animator)
    {
        _target = target;
        _health = health;
        _attackRange = attackRange;
        _navMeshAgent = navMeshAgent;
        _animator = animator;
    }
    public PlayerMovement Target => _target;
    public ReactiveProperty<float> Health => _health;
    public float AttackRange => _attackRange;
    public NavMeshAgent NavMeshAgent => _navMeshAgent;
    public Animator Animator => _animator;

    private PlayerMovement _target;
    private ReactiveProperty<float> _health;
    private float _attackRange;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
}
