using UnityEngine;

// テスト用の的。ブレイク値が届くかも確認できるよう IBreakable も実装している。
public class DamageableDummy : MonoBehaviour, IDamageable, IBreakable
{
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth;
    public int CurrentHealth => _currentHealth;
    // 受け取ったブレイク値の合計。ブレイク状態への移行は敵側の実装なので、ここでは数えるだけ。
    [SerializeField] private float _accumulatedBreak;
    private void Awake() => _currentHealth = _maxHealth;
    public void TakeDamage(int damage)
    {
        if (_currentHealth <= 0) return;
        int applied = Mathf.Max(0, damage);
        _currentHealth = Mathf.Max(0, _currentHealth - applied);
        Debug.Log($"[Dummy] {name}: damage={applied}, HP={_currentHealth}/{_maxHealth}", this);
    }

    public void TakeBreak(float breakValue)
    {
        if (breakValue <= 0f) return;
        _accumulatedBreak += breakValue;
        Debug.Log($"[Dummy] {name}: break=+{breakValue:0.00}, total={_accumulatedBreak:0.00}", this);
    }
}
