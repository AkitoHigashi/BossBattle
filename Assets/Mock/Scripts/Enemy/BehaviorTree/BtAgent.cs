using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class BtAgent : MonoBehaviour
{
    [SerializeField] private BehaviorTree _behaviorTree;

    private NavMeshAgent navMeshAgent;
    private BehaviorTree _runtimeTree;
    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        _runtimeTree = _behaviorTree;
    }
}
