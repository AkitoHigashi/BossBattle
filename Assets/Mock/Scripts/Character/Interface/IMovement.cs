
using UnityEngine;

public interface IMovement
{
    void Move(MovementData movementData);
}

public struct MovementData
{
    public Vector3 Direction;
}