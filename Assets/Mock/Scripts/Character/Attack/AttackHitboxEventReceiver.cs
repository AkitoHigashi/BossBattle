using UnityEngine;

public class AttackHitboxEventReceiver : MonoBehaviour
{
    [SerializeField] private AttackHitbox[] _hitboxes;

    public void EnableAttackHitbox()
    {
        for (int i = 0; i < _hitboxes.Length; i++)
        {
            if (_hitboxes[i] != null)
                _hitboxes[i].EnableHitbox();
        }
    }

    public void DisableAttackHitbox()
    {
        for (int i = 0; i < _hitboxes.Length; i++)
        {
            if (_hitboxes[i] != null)
                _hitboxes[i].DisableHitbox();
        }
    }
}
