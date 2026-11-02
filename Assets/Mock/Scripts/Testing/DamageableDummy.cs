using UnityEngine;

public class DamageableDummy : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth;
    public int CurrentHealth => _currentHealth;
    private void Awake() => _currentHealth = _maxHealth;
    public void TakeDamage(int damage)
    {
        if (_currentHealth <= 0) return;
        int applied = Mathf.Max(0, damage);
        _currentHealth = Mathf.Max(0, _currentHealth - applied);
        Debug.Log($"[Dummy] {name}: damage={applied}, HP={_currentHealth}/{_maxHealth}", this);
    }
}
