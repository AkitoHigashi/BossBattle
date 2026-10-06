using UnityEngine;

[DisallowMultipleComponent]
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private InputBuffer _inputBuffer;
    [SerializeField] private CharacterComposition _characterComposition;
    [SerializeField] private PlayerController _playerController;

    public bool IsInitialized { get; private set; }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (IsInitialized)
            return;

        if (_inputBuffer == null || _characterComposition == null || _playerController == null)
            throw new System.InvalidOperationException("GameInitializerにInputBuffer、CharacterComposition、PlayerControllerを設定してください。");

        _inputBuffer.Initialize();
        _characterComposition.Initialize();
        _playerController.Initialize(_inputBuffer, _characterComposition.Character);

        IsInitialized = true;
    }
}
