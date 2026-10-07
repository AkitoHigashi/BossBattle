using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class BtAgent : MonoBehaviour
{
    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public float Speed => navMeshAgent.speed;
    public float AttackRange => _attackRange;

    [SerializeField] private BehaviorTree _behaviorTree;
    [SerializeField] private float _attackRange = 1.5f;
    private NavMeshAgent navMeshAgent;
    private BehaviorTree _runtimeTree;
    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        _runtimeTree = _behaviorTree;
    }

    private void Update()
    {
        _runtimeTree.Update();
    }
}
