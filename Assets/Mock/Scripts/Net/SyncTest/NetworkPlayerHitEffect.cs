
using System.Collections;
using FishNet.Object;
using UnityEngine;

/// <summary>
///     ObserversRpcを使用して被弾演出を同期するクラス
/// </summary>
public class NetworkPlayerHitEffect : NetworkBehaviour
{
    [SerializeField] private Renderer _targetRenderer;
    [SerializeField] private Color _hitColor = Color.red;
    [SerializeField] private float _effectDuration = 0.2f;

    private MaterialPropertyBlock _propertyBlock;
    private Color _defaultColor;
    private Coroutine _effectCoroutine;
    private int _colorPropertyId;

    /// <summary>
    ///     描画に使用する情報を取得する
    /// </summary>
    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (_targetRenderer == null)
        {
            _targetRenderer = GetComponentInChildren<Renderer>();
        }

        _colorPropertyId = Shader.PropertyToID("_BaseColor");

        if (_targetRenderer == null)
        {
            Debug.LogError("被弾演出用Rendererがありません。");
            return;
        }

        Material material = _targetRenderer.sharedMaterial;

        if (material.HasProperty(_colorPropertyId))
        {
            _defaultColor = material.GetColor(_colorPropertyId);
        }
        else
        {
            _colorPropertyId = Shader.PropertyToID("_Color");
            _defaultColor = material.HasProperty(_colorPropertyId)
                ? material.GetColor(_colorPropertyId)
                : Color.white;
        }
    }

    /// <summary>
    ///     サーバーから被弾演出を通知する
    /// </summary>
    public void NotifyHit()
    {
        if (!IsServerInitialized)
        {
            return;
        }

        PlayHitEffectObserversRpc();
    }

    /// <summary>
    ///     観測中の全クライアントで被弾演出を再生する
    /// </summary>
    [ObserversRpc]
    private void PlayHitEffectObserversRpc()
    {
        if (_targetRenderer == null)
        {
            return;
        }

        if (_effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
        }

        _effectCoroutine = StartCoroutine(PlayHitEffectCoroutine());

        Debug.Log(
            $"被弾演出を再生 | ObjectId: {ObjectId} | IsOwner: {IsOwner}"
        );
    }

    /// <summary>
    ///     一定時間だけCubeの色を変更する
    /// </summary>
    private IEnumerator PlayHitEffectCoroutine()
    {
        SetColor(_hitColor);

        yield return new WaitForSeconds(_effectDuration);

        SetColor(_defaultColor);
        _effectCoroutine = null;
    }

    /// <summary>
    ///     Rendererの色を変更する
    /// </summary>
    private void SetColor(Color color)
    {
        _targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_colorPropertyId, color);
        _targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    ///     無効化時に元の色へ戻す
    /// </summary>
    private void OnDisable()
    {
        if (_targetRenderer != null && _propertyBlock != null)
        {
            SetColor(_defaultColor);
        }
    }
}
