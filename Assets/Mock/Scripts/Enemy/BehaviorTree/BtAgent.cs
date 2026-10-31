using R3;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(AttackPredictionView)), DisallowMultipleComponent]
public class BtAgent : MonoBehaviour
{
    [SerializeField] private float _healthValue = 100f;
    [SerializeField] private BehaviorTree _behaviorTree;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private int _attackDamage = 10;
    [SerializeField] private CharacterMotor _target;

    private ReactiveProperty<float> _health;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    private BehaviorTree _runtimeTree;
    private BlackBoard _blackBoard;
    private AttackPredictionView _predictionView;

    /// <summary>設定されている元のツリーアセット</summary>
    public BehaviorTree SourceTree => _behaviorTree;

    /// <summary>このエージェント専用に複製した実行中のツリー</summary>
    public BehaviorTree RuntimeTree => _runtimeTree;

    /// <summary>実行中のツリーに配っている黒板。Start前は null</summary>
    public BlackBoard BlackBoard => _blackBoard;

    private void Awake()
    {
        if (_behaviorTree == null)
        {
            Debug.LogError("BehaviorTreeが設定されていません。", this);
            enabled = false;// 無効化しておけばStartもUpdateも呼ばれない
            return;
        }

        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _predictionView = GetComponent<AttackPredictionView>();
        _runtimeTree = _behaviorTree.Clone();
    }

    private void Start()
    {
        _health = new ReactiveProperty<float>(_healthValue);
        _blackBoard = new BlackBoard(_target, _health, _attackRange, _attackDamage, _navMeshAgent, _animator, transform, _predictionView);
        _runtimeTree.Initialize(_blackBoard);
    }

    private void Update()
    {
        _runtimeTree.Update();
    }

    private void OnDisable()
    {
        // 無効化中に追跡などが動き続けないよう、途中の処理を止める。再び有効になったら最初から評価する
        if (_runtimeTree != null)
        {
            _runtimeTree.AbortAll();
        }
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
