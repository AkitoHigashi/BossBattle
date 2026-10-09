public class Character : IDamagable
{

    public Character(
        HealthEntity health,
        Status status,
        IMovement movement,
        IAttackRunner attackRunner,
        IForceReceiver forceReceiver)
    {
        _health = health ?? throw new System.ArgumentNullException(nameof(health));
        _status = status ?? throw new System.ArgumentNullException(nameof(status));
        _movement = movement ?? throw new System.ArgumentNullException(nameof(movement));
        _attackRunner = attackRunner;
        _forceReceiver = forceReceiver;
    }

    public HealthEntity Health => _health;
    public Status Status => _status;
    public IForceReceiver ForceReceiver => _forceReceiver;

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
        if (_attackRunner == null)
            return;
        _attackRunner.Running(attackData);
    }

    private HealthEntity _health;
    private Status _status;
    private IMovement _movement;
    private IAttackRunner _attackRunner;
    private IForceReceiver _forceReceiver;
}
