using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterComposition))]
[RequireComponent(typeof(AttackResolver))]
[RequireComponent(typeof(MotionSystem))]
public class AttackTestDriver : MonoBehaviour
{
    [SerializeField] private AttackDefinition _attackDefinition;
    [SerializeField, Min(0)] private int _fallbackDamage = 1;
    [SerializeField] private KeyCode _attackKey = KeyCode.Space;
    [SerializeField] private Collider _targetCollider;
    [SerializeField, Min(0f)] private float _searchRadius = 2f;
    [SerializeField] private LayerMask _targetLayers = ~0;

    private CharacterComposition _composition;
    private AttackResolver _resolver;
    private MotionSystem _motionSystem;

    private void Awake()
    {
        _composition = GetComponent<CharacterComposition>();
        _resolver = GetComponent<AttackResolver>();
        _motionSystem = GetComponent<MotionSystem>();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        if (IsAttackPressed())
        {
            Debug.Log($"{nameof(AttackTestDriver)}: Attack input received.");
            ExecuteTestAttack();
        }
    }

    [ContextMenu("Execute Test Attack")]
    public void ExecuteTestAttack()
    {
        EnsureInitialized();

        Collider target = _targetCollider != null ? _targetCollider : FindTargetInFront();
        if (target == null)
        {
            Debug.LogWarning($"{nameof(AttackTestDriver)}: Target collider was not found.");
            return;
        }

        CharacterComposition targetComposition = target.GetComponentInParent<CharacterComposition>();
        if (targetComposition == null)
        {
            Debug.LogWarning($"{nameof(AttackTestDriver)}: Target does not have {nameof(CharacterComposition)}.");
            return;
        }

        targetComposition.Initialize();

        AttackData attackData = _attackDefinition != null
            ? new AttackData(_attackDefinition)
            : new AttackData(null, default, _fallbackDamage, null, null);

        Character targetCharacter = targetComposition.Character;
        int beforeHealth = targetCharacter != null ? targetCharacter.Health.CurrentValue : -1;

        _motionSystem.ExitMotion();
        _composition.Character.Attack(attackData);
        _resolver.Resolve(target);
        _motionSystem.ExitMotion();

        if (targetCharacter != null)
        {
            Debug.Log(
                $"{nameof(AttackTestDriver)}: Attack hit {targetCharacter}. " +
                $"HP {beforeHealth} -> {targetCharacter.Health.CurrentValue}");
        }
        else
        {
            Debug.LogWarning($"{nameof(AttackTestDriver)}: Target character was not initialized.");
        }
    }

    private void EnsureInitialized()
    {
        if (_composition == null)
            _composition = GetComponent<CharacterComposition>();
        if (_resolver == null)
            _resolver = GetComponent<AttackResolver>();
        if (_motionSystem == null)
            _motionSystem = GetComponent<MotionSystem>();

        _composition.Initialize();
    }

    private bool IsAttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(_attackKey))
            return true;
#endif

        return false;
    }

    private Collider FindTargetInFront()
    {
        Vector3 center = transform.position + transform.forward * _searchRadius * 0.5f;
        Collider[] hits = Physics.OverlapSphere(center, _searchRadius, _targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            CharacterComposition target = hits[i].GetComponentInParent<CharacterComposition>();
            if (target == null || target == _composition)
                continue;

            target.Initialize();
            return hits[i];
        }

        return null;
    }
}
