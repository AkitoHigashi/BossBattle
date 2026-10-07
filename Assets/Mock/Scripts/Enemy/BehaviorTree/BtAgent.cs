using R3;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent)), DisallowMultipleComponent]
public class BtAgent : MonoBehaviour
{
    [SerializeField] private float _healthValue = 100f;
    [SerializeField] private BehaviorTree _behaviorTree;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private PlayerMovement _target;

    private ReactiveProperty<float> _health;
    private NavMeshAgent _navMeshAgent;
    private BehaviorTree _runtimeTree;
    private BlackBoard _blackBoard;

    private void Awake()
    {
        if (_behaviorTree == null)
        {
            Debug.LogError("BehaviorTreeが設定されていません。", this);
            enabled = false;// 無効化しておけばStartもUpdateも呼ばれない
            return;
        }

        _navMeshAgent = GetComponent<NavMeshAgent>();
        _runtimeTree = _behaviorTree.Clone();
    }

    private void Start()
    {
        _health = new ReactiveProperty<float>(_healthValue);
        _blackBoard = new BlackBoard(_target, _health, _attackRange, _navMeshAgent);
        _runtimeTree.Initialize(_blackBoard);
    }

    private void Update()
    {
        _runtimeTree.Update();
    }

    private void OnDestroy()
    {
        if (_runtimeTree != null)
        {
            Destroy(_runtimeTree);// ツリーのOnDestroyが複製したNodeを片付ける
        }

        _health?.Dispose();
    }
}
