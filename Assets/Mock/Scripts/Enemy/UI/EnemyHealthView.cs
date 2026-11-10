using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthView : MonoBehaviour
{
    [SerializeField] Image _healthBar;

    public void UpdateHealth(float healthPercentage)
    {
        _healthBar.fillAmount = healthPercentage;
    }
}
