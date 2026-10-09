using R3;
using UnityEngine;
using UnityEngine.AI;

public class BlackBoard
{
    public BlackBoard(CharacterMotor target,
        ReactiveProperty<float> health, 
        float attackRange, 
        NavMeshAgent navMeshAgent,
        Animator animator,
        Transform transform)
    {
        _target = target;
        _health = health;
        _attackRange = attackRange;
        _navMeshAgent = navMeshAgent;
        _animator = animator;
        _transform = transform; 
    }
    public CharacterMotor Target => _target;
    public ReactiveProperty<float> Health => _health;
    public float AttackRange => _attackRange;
    public NavMeshAgent NavMeshAgent => _navMeshAgent;
    public Animator Animator => _animator;
    public Transform Transform => _transform;

    private CharacterMotor _target;
    private ReactiveProperty<float> _health;
    private float _attackRange;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    private Transform _transform;
}
