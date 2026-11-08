
using System;

public class HealthEntity
{
    public HealthEntity(int maxHealth)
    {
        MaxValue = maxHealth;
        CurrentValue = maxHealth;
    }
    public int MaxValue { get; private set; }
    public int CurrentValue { get; private set; }
    public bool IsDead => CurrentValue <= 0;
    public event Action<int> OnHealthChanged;
    /// <summary>HPが0になった瞬間に一度だけ通知される。</summary>
    public event Action OnDied;

    public void TakeDamage(int damage)
    {
        // 死亡後のダメージと、回復になってしまう負のダメージは無視する
        if (IsDead || damage <= 0)
            return;

        CurrentValue = Math.Max(0, CurrentValue - damage);
        OnHealthChanged?.Invoke(CurrentValue);

        if (IsDead)
            OnDied?.Invoke();
    }
}
