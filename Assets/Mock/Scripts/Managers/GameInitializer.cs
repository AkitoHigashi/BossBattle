using UnityEngine;

[DisallowMultipleComponent]
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private InputBuffer _inputBuffer;
    [SerializeField] private CharacterComposition _characterComposition;
    [SerializeField] private PlayerController _playerController;
    [Tooltip("全スキルのクールダウン表示（任意）。未設定でもゲームは動作し、表示が出ないだけ")]
    [SerializeField] private SkillCooldownView _skillCooldownView;
    [Tooltip("プレイヤーのHPバー（任意）。未設定でもゲームは動作し、表示が出ないだけ")]
    [SerializeField] private PlayerHealthView _playerHealthView;

    public bool IsInitialized { get; private set; }
    // プレイヤーがDestroyされた後でも購読を解除できるよう、HP自体を保持する
    private HealthEntity _playerHealth;

    public void Initialize()
    {
        if (IsInitialized)
            return;

        if (_inputBuffer == null || _characterComposition == null || _playerController == null)
            throw new System.InvalidOperationException("GameInitializerにInputBuffer、CharacterComposition、PlayerControllerを設定してください。");

        _inputBuffer.Initialize();
        _characterComposition.Initialize();
        _playerController.Initialize(_inputBuffer, _characterComposition.Character, _characterComposition.CharacterData, _characterComposition.SkillSystem);

        // クールダウン表示は SkillSystem（残り時間）と PlayerController（スキルとキーの対応）の両方が必要なので、
        // 両方の初期化が終わったここで渡す。
        if (_skillCooldownView != null && _characterComposition.SkillSystem != null)
            _skillCooldownView.Initialize(_characterComposition.SkillSystem, _playerController.GetSkillSlots());

        _playerHealth = _characterComposition.Character.Health;
        _playerHealth.OnHealthChanged += OnPlayerHealthChanged;
        _playerHealth.OnDied += OnPlayerDied;
        // 被弾前から満タンの表示にしておく
        OnPlayerHealthChanged(_playerHealth.CurrentValue);

        IsInitialized = true;
    }

    private void Start()
    {
        Initialize();
    }

    private void OnPlayerHealthChanged(int value)
    {
        if (_playerHealthView != null)
            _playerHealthView.UpdateHealth((float)value / _playerHealth.MaxValue);
    }

    private void OnPlayerDied()
    {
        // 入力を先に止める。PlayerControllerのOnDisableで購読解除・停止・スキル中断まで行われる
        _playerController.enabled = false;
        Debug.Log("[Player] 死亡", _characterComposition);
        Destroy(_characterComposition.gameObject);
    }

    private void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
            _playerHealth.OnDied -= OnPlayerDied;
        }
    }
}
