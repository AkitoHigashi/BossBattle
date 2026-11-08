using R3;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(AttackPredictionView)), DisallowMultipleComponent]
public class BtAgent : MonoBehaviour
{

    /// <summary>設定されている元のツリーアセット</summary>
    public BehaviorTree SourceTree => _behaviorTree;

    /// <summary>このエージェント専用に複製した実行中のツリー</summary>
    public BehaviorTree RuntimeTree => _runtimeTree;

    /// <summary>実行中のツリーに配っている黒板。Start前は null</summary>
    public BlackBoard BlackBoard => _blackBoard;

    /// <summary>敵のHP。被弾処理（EnemyDamageReceiver）から減らす。Awake以降は常に存在する</summary>
    public HealthEntity Health => _healthEntity;

    [SerializeField] private float _healthValue = 100f;
    [SerializeField] private BehaviorTree _behaviorTree;
    [SerializeField] private int _attackDamage = 10;
    [SerializeField] private CharacterMotor _target;
    [SerializeField, Min(0f), Tooltip("死亡してからDestroyするまでの秒数")]
    private float _destroyDelay;

    private HealthEntity _healthEntity;
    // ノードが購読できるよう、HealthEntityの値をReactivePropertyへ流す
    private ReactiveProperty<float> _health;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    private BehaviorTree _runtimeTree;
    private BlackBoard _blackBoard;
    private AttackPredictionView _predictionView;


    private void Awake()
    {
        // ツリー未設定でも被弾・死亡はできるよう、HPは先に用意する
        _healthEntity = new HealthEntity(Mathf.Max(1, Mathf.RoundToInt(_healthValue)));
        _health = new ReactiveProperty<float>(_healthEntity.CurrentValue);
        _healthEntity.OnHealthChanged += OnHealthChanged;
        _healthEntity.OnDied += OnDied;

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
        _blackBoard = new BlackBoard(_target, _health, _attackDamage, _navMeshAgent, _animator, transform, _predictionView);
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

    private void OnHealthChanged(int value)
    {
        _health.Value = value;
    }

    private void OnDied()
    {
        // 無効化でツリーを止め、移動中の経路も止めてからDestroyする
        enabled = false;
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
            _navMeshAgent.isStopped = true;

        Debug.Log($"{name}：死亡", this);
        Destroy(gameObject, _destroyDelay);
    }

    private void OnDestroy()
    {
        if (_runtimeTree != null)
        {
            Destroy(_runtimeTree);// ツリーのOnDestroyが複製したNodeを片付ける
        }

        if (_healthEntity != null)
        {
            _healthEntity.OnHealthChanged -= OnHealthChanged;
            _healthEntity.OnDied -= OnDied;
        }
        _health?.Dispose();
    }
}
