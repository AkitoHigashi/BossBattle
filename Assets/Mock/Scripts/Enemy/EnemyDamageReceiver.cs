using UnityEngine;

/// <summary>
/// 敵がプレイヤーの攻撃（剣・飛び道具）を受け取る窓口。
/// AttackData.ApplyTo から呼ばれ、BtAgent のHPを減らし、ブレイク値を貯める。
/// </summary>
/// <remarks>
/// BtAgent はツリーの実行、こちらは被弾の受け付けと役割を分けている。
/// ブレイク状態への移行は未実装で、今は値を貯めるだけ。
/// </remarks>
[RequireComponent(typeof(BtAgent)), DisallowMultipleComponent]
public class EnemyDamageReceiver : MonoBehaviour, IDamageable, IBreakable
{
    /// <summary>これまでに受け取ったブレイク値の合計</summary>
    public float AccumulatedBreak => _accumulatedBreak;

    [SerializeField] private float _accumulatedBreak;

    [Header("表示")]
    [Tooltip("当たったときに、ダメージとブレイク値を頭上に表示する")]
    [SerializeField] private bool _showPopup = true;
    [Tooltip("表示する位置（足元からのずれ）")]
    [SerializeField] private Vector3 _popupOffset = new Vector3(0f, 2f, 0f);

    private BtAgent _agent;

    private void Awake() => _agent = GetComponent<BtAgent>();

    public void TakeDamage(int damage)
    {
        var health = _agent.Health;
        int applied = Mathf.Max(0, damage);
        if (health == null || health.IsDead || applied <= 0)
            return;

        health.TakeDamage(applied);
        Debug.Log($"[Enemy] {name}: damage={applied}, HP={health.CurrentValue}/{health.MaxValue}", this);

        // 連続ヒットで数字が重ならないよう、少し横にずらす。
        if (_showPopup)
            DamagePopup.Show(PopupPosition(Random.Range(-0.4f, 0.4f)), applied.ToString(), Color.white);
    }

    public void TakeBreak(float breakValue)
    {
        // 死亡した敵にはブレイク値も貯めない（同じ命中でダメージの直後に呼ばれる）
        if (breakValue <= 0f || _agent.Health == null || _agent.Health.IsDead)
            return;

        _accumulatedBreak += breakValue;
        Debug.Log($"[Enemy] {name}: break=+{breakValue:0.00}, total={_accumulatedBreak:0.00}", this);

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
