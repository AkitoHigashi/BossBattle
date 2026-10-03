using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour, IMovement
{
    public void Move(Vector3 direction)
    {
        _rb.MovePosition(_rb.position + direction * Time.fixedDeltaTime);
    }

    private Rigidbody _rb;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

}
