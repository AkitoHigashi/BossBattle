

using UnityEngine;

public class Character
{

    public Character(Health health, IMovement movement)
    {
        _health = health;
        _movement = movement;
    }
    public void TakeDamage(int damage)
    {
        _health.TakeDamage(damage);
    }

    public void Move(Vector3 direction)
    {
        _movement.Move(direction);
    }

   private Health _health;
   private IMovement _movement;
}
