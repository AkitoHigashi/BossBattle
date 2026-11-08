using UnityEngine;

// テスト用の的。ブレイク値が届くかも確認できるよう IBreakable も実装している。
public class DamageableDummy : MonoBehaviour, IDamageable, IBreakable
{
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth;
    public int CurrentHealth => _currentHealth;
    // 受け取ったブレイク値の合計。ブレイク状態への移行は敵側の実装なので、ここでは数えるだけ。
    [SerializeField] private float _accumulatedBreak;

    [Header("テスト用表示")]
    [Tooltip("当たったときに、ダメージとブレイク値を頭上に表示する")]
    [SerializeField] private bool _showPopup = true;
    [Tooltip("表示する位置（足元からのずれ）")]
    [SerializeField] private Vector3 _popupOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("HP が 0 になったら全回復する（何度でも攻撃を試せるように）")]
    [SerializeField] private bool _resetWhenDefeated = true;

    private void Awake() => _currentHealth = _maxHealth;

    public void TakeDamage(int damage)
    {
        if (_currentHealth <= 0) return;
        int applied = Mathf.Max(0, damage);
        _currentHealth = Mathf.Max(0, _currentHealth - applied);
        Debug.Log($"[Dummy] {name}: damage={applied}, HP={_currentHealth}/{_maxHealth}", this);

        // 連続ヒットで数字が重ならないよう、少し横にずらす。
        if (_showPopup)
            DamagePopup.Show(PopupPosition(Random.Range(-0.4f, 0.4f)), applied.ToString(), Color.white);

        if (_currentHealth <= 0 && _resetWhenDefeated)
            _currentHealth = _maxHealth;
    }

    public void TakeBreak(float breakValue)
    {
        if (breakValue <= 0f) return;
        _accumulatedBreak += breakValue;
        Debug.Log($"[Dummy] {name}: break=+{breakValue:0.00}, total={_accumulatedBreak:0.00}", this);

        // ダメージの数字と重ならないよう、少し下に小さく出す。
        if (_showPopup)
            DamagePopup.Show(PopupPosition(0f) + Vector3.down * 0.4f, $"BREAK +{breakValue:0.##}",
                new Color(1f, 0.6f, 0.15f), fontSize: 18);
    }

    private Vector3 PopupPosition(float sideOffset)
    {
        return transform.position + _popupOffset + transform.right * sideOffset;
    }
}
