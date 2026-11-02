using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     ServerRpcとSyncVarでHP・ダメージ同期を検証するクラス
/// </summary>
public class NetworkPlayerHealth : NetworkBehaviour
{
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _attackDamage = 25;

    private NetworkPlayerHitEffect _hitEffect;

    private readonly SyncVar<int> _health = new SyncVar<int>();

    /// <summary>
    ///     HP変更イベントを登録する
    /// </summary>
    private void Awake()
    {
        _health.OnChange += OnHealthChanged;
        _hitEffect = GetComponent<NetworkPlayerHitEffect>();
    }

    /// <summary>
    ///     HP変更イベントを解除する
    /// </summary>
    private void OnDestroy()
    {
        _health.OnChange -= OnHealthChanged;
    }

    /// <summary>
    ///     サーバー側でHPを初期化する
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        _health.Value = _maxHealth;
    }

    /// <summary>
    ///     所有者のSpaceキー入力を検出する
    /// </summary>
    private void Update()
    {
        if (!IsClientInitialized || !IsOwner)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null ||
            !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        AttackServerRpc();
    }

    /// <summary>
    ///     サーバーへ攻撃を要求する
    /// </summary>
    [ServerRpc]
    private void AttackServerRpc()
    {
        NetworkPlayerHealth[] players =
            FindObjectsByType<NetworkPlayerHealth>();

        foreach (NetworkPlayerHealth player in players)
        {
            if (player == this || !player.IsServerInitialized)
            {
                continue;
            }

            player.ApplyDamage(_attackDamage);
            break;
        }
    }

    /// <summary>
    ///     サーバー側でダメージを適用する
    /// </summary>
    /// <param name="damage"> ダメージ量 </param>
    public void ApplyDamage(int damage)
    {
        if (!IsServerInitialized || damage <= 0)
        {
            return;
        }

        int previousHealth = _health.Value;

        _health.Value = Mathf.Max(
            0,
            previousHealth - damage
        );

        if (_health.Value == previousHealth)
        {
            return;
        }

        // HPが実際に減少したときだけ被弾演出を通知する
        _hitEffect?.NotifyHit();
    }

    /// <summary>
    ///     HPが変更されたときに呼び出される
    /// </summary>
    /// <param name="previous"> 変更前のHP </param>
    /// <param name="next"> 変更後のHP </param>
    /// <param name="asServer"> サーバー側の変更通知か </param>
    private void OnHealthChanged(
        int previous,
        int next,
        bool asServer)
    {
        Debug.Log(
            $"HP変更 | ObjectId: {ObjectId} | " +
            $"{previous} → {next} | " +
            $"AsServer: {asServer}"
        );
    }
}
