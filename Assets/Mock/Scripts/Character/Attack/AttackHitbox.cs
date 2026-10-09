using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private AttackResolver _resolver;
    [SerializeField] private bool _disableColliderOnAwake = true;

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        if (_resolver == null)
            _resolver = GetComponentInParent<AttackResolver>();
        if (_disableColliderOnAwake)
            _collider.enabled = false;
    }

    public void EnableHitbox()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();

        _collider.enabled = true;
    }

    public void DisableHitbox()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();

        _collider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_resolver == null)
            return;

        _resolver.Resolve(other);
    }
}
