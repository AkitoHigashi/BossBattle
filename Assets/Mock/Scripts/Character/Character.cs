

using System;
using UnityEngine;

public class Character
{

    public Character(HealthEntity health, Status status, IMovement movement)
    {
        _health = health;
        _status = status;
        _movement = movement;
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
        _attackSystem.Attack(attackData);
    }

   private HealthEntity _health;
   private Status _status;
   private IMovement _movement;
   private IAttackSystem _attackSystem;
}
