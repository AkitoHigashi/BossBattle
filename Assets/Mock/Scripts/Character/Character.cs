


public class Character
{

    public Character(HealthEntity health, Status status, IMovement movement)
        : this(health, status, movement, null)
    {
    }

    public Character(HealthEntity health, Status status, IMovement movement, IAttackSystem attackSystem)
    {
        _health = health ?? throw new System.ArgumentNullException(nameof(health));
        _status = status ?? throw new System.ArgumentNullException(nameof(status));
        _movement = movement ?? throw new System.ArgumentNullException(nameof(movement));
        _attackSystem = attackSystem;
    }

    public HealthEntity Health => _health;
    public Status Status => _status;

    public void TakeDamage(int damage)
    {
        _health.TakeDamage(damage);
    }

    public void Move(MovementData movementData)
    {
        _movement.Move(movementData);
    }

    public void Attack(AttackData attackData)
    {
        if (_attackSystem == null)
            throw new System.InvalidOperationException("No attack system has been configured for this character.");
        _attackSystem.Attack(attackData);
    }

    private HealthEntity _health;
    private Status _status;
    private IMovement _movement;
    private IAttackSystem _attackSystem;
}
