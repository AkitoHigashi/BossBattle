using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MotionDetecotr : MonoBehaviour
{
    private void Awake()
    {
        _motionSystem = GetComponentInParent<IMotionSystem>();
    }

    public void OnTriggerEnter(Collider other)
    {
        if(_motionSystem != null && _motionSystem.GetIsMotionActive())
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                Debug.Log($"MotionDetector: {player.name} entered the motion area.");
            }
        }
    }

    private IMotionSystem _motionSystem;

}
