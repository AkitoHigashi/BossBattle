using R3;
using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class BtAgent : MonoBehaviour
{
    [SerializeField] private float _healthValue = 100f;
    [SerializeField] private BehaviorTree _behaviorTree;
    [SerializeField] private float _attackRange = 1.5f;
    private ReactiveProperty<float> _health;
    private NavMeshAgent _navMeshAgent;
    private BehaviorTree _runtimeTree;
    private BlackBoard _blackBoard;
    private void Awake()
    {
        if (_behaviorTree == null)
        {
            Debug.LogError("BehaviorTreeが設定されていません。");
        }
        _health = new ReactiveProperty<float>(_healthValue);
        _blackBoard = new BlackBoard(_health, _attackRange, _navMeshAgent);
    }
    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _runtimeTree = _behaviorTree;
        _runtimeTree.Initialize(_blackBoard);
    }

    private void Update()
    {
        _runtimeTree.Update();
    }
}
