using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthView : MonoBehaviour
{
    [SerializeField] Image _healthBar;

    public void UpdateHealth(float healthPercentage)
    {
        _healthBar.fillAmount = healthPercentage;
    }
}
