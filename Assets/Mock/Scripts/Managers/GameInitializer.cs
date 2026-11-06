using UnityEngine;

[DisallowMultipleComponent]
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private InputBuffer _inputBuffer;
    [SerializeField] private CharacterComposition _characterComposition;
    [SerializeField] private PlayerController _playerController;
    [Tooltip("全スキルのクールダウン表示（任意）。未設定でもゲームは動作し、表示が出ないだけ")]
    [SerializeField] private SkillCooldownView _skillCooldownView;

    public bool IsInitialized { get; private set; }

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

        IsInitialized = true;
    }

    private void Start()
    {
        Initialize();
    }
}
