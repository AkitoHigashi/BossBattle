
using System;

public class Health
{
    public Health(int maxHealth)
    {
        MaxValue = maxHealth;
        CurrentValue = maxHealth;
    }
    public int MaxValue { get; private set; }
    public int CurrentValue { get; private set; }
    public event Action<int> OnHealthChanged;

    public void TakeDamage(int damage)
    {
        CurrentValue -= damage;
        if (CurrentValue < 0)
        {
            CurrentValue = 0;
        }
        OnHealthChanged?.Invoke(CurrentValue);
    }
}
