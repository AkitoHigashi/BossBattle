using System;
using UnityEngine;

public class CharacterMovement : IMovement
{
    private readonly CharacterData _data;
    private readonly CharacterMotor _motor;

    public CharacterMovement(CharacterData data, CharacterMotor motor)
    {
        _data = data != null ? data : throw new ArgumentNullException(nameof(data));
        _motor = motor != null ? motor : throw new ArgumentNullException(nameof(motor));
    }

    public void Move(MovementData movementData)
    {
        Vector3 direction = movementData.Direction;
        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);
        _motor.SetLocomotionVelocity(direction * _data.MoveSpeed);
    }
}
