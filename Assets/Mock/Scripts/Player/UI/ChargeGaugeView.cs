using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 溜めゲージを画面に描く。キャラクターの頭上に追従する横長のバーで、
/// 溜め斬りつけではジャストゾーンを重ねて表示する。
/// </summary>
/// <remarks>
/// ChargeGauge（データ）を毎フレーム読むだけで、溜めの判定や威力には一切関わらない。
/// 現状は仮の見た目なので、Canvas や Image を実行時にコードで組み立てている（Prefab 不要）。
/// 本番の見た目を作るときは、Prefab の参照を受け取る形に差し替えればよく、ChargeAction 側の変更は要らない。
/// </remarks>
[DisallowMultipleComponent]
public class ChargeGaugeView : MonoBehaviour
{
    [Tooltip("頭上位置の計算に使うカメラ。未設定なら Camera.main を使う")]
    [SerializeField] private Camera _camera;
    [Tooltip("キャラクターの足元からゲージを表示する位置までのずれ（ワールド座標）")]
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);
    [Tooltip("ゲージの大きさ（ピクセル）")]
    [SerializeField] private Vector2 _size = new Vector2(140f, 14f);

    [Header("色")]
    [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color _fillColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("ジャストゾーンの色。溜め量が見えるよう半透明にしている")]
    [SerializeField] private Color _justZoneColor = new Color(0.2f, 1f, 1f, 0.45f);
    [Tooltip("溜め量がジャストゾーン内にあるときのゲージの色。「今離せば成功」を伝える")]
    [SerializeField] private Color _fillInJustZoneColor = new Color(0.3f, 1f, 1f, 1f);

    private ChargeGauge _gauge;
    private Transform _target;
    private GameObject _canvasObject;
    private RectTransform _root;
    private RectTransform _fill;
    private Image _fillImage;
    private RectTransform _justZone;

    /// <summary>
    /// 表示するゲージと、追従するキャラクターを設定する。CharacterComposition の初期化時に呼ばれる。
    /// </summary>
    public void Initialize(ChargeGauge gauge, Transform target)
    {
        _gauge = gauge ?? throw new System.ArgumentNullException(nameof(gauge));
        _target = target != null ? target : throw new System.ArgumentNullException(nameof(target));

        if (_canvasObject == null)
            BuildUI();

        _root.gameObject.SetActive(false);
    }

    // キャラクターの移動（FixedUpdate）とカメラの移動が終わった後に位置を合わせ、ゲージが遅れて揺れるのを防ぐ。
    private void LateUpdate()
    {
        if (_gauge == null)
            return;

        // 溜めていない、またはキャラクターが画面外（カメラの後ろ）なら表示しない。
        Vector3 screenPosition = Vector3.zero;
        bool visible = _gauge.IsVisible && TryGetScreenPosition(out screenPosition);
        if (_root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);
        if (!visible)
            return;

        _root.position = screenPosition;

        // 塗りは幅を変えるのではなく、右端のアンカーを溜め量の位置へ動かして表現する。
        // Image の Filled タイプはスプライトが必要なため、スプライトなしで済むこの方法にしている。
        _fill.anchorMax = new Vector2(_gauge.Progress, 1f);
        _fillImage.color = _gauge.IsInJustZone ? _fillInJustZoneColor : _fillColor;

        _justZone.gameObject.SetActive(_gauge.HasJustZone);
        if (_gauge.HasJustZone)
        {
            _justZone.anchorMin = new Vector2(_gauge.JustZoneStart, 0f);
            _justZone.anchorMax = new Vector2(_gauge.JustZoneEnd, 1f);
        }
    }

    private bool TryGetScreenPosition(out Vector3 screenPosition)
    {
        screenPosition = Vector3.zero;
        Camera camera = _camera != null ? _camera : Camera.main;
        if (camera == null || _target == null)
            return false;

        screenPosition = camera.WorldToScreenPoint(_target.position + _worldOffset);
        // z が負ならカメラの後ろにいる。そのまま描くと画面上の反対側に出てしまうため隠す。
        return screenPosition.z > 0f;
    }

    /// <summary>
    /// 背景・ジャストゾーン・塗りの順に重ねたゲージを組み立てる。後に作ったものほど手前に描かれる。
    /// </summary>
    private void BuildUI()
    {
        _canvasObject = new GameObject("ChargeGaugeCanvas", typeof(Canvas), typeof(CanvasScaler));
        // 親子付けしても Screen Space Overlay の Canvas は親の移動・回転の影響を受けない。
        // 階層を整理し、キャラクターと一緒に破棄されるよう子にしている。
        _canvasObject.transform.SetParent(transform, false);
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 他の UI より手前に出す。
        canvas.sortingOrder = 100;

        _root = CreateRect("ChargeGauge", _canvasObject.transform);
        _root.sizeDelta = _size;
        // 頭上の点がゲージの下端中央に来るようにする。
        _root.pivot = new Vector2(0.5f, 0f);
        CreateImage(_root, _backgroundColor);

        _justZone = CreateRect("JustZone", _root);
        Stretch(_justZone);
        CreateImage(_justZone, _justZoneColor);

        _fill = CreateRect("Fill", _root);
        Stretch(_fill);
        _fill.anchorMax = new Vector2(0f, 1f);
        _fillImage = CreateImage(_fill, _fillColor);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        // 表示専用なので、クリック判定の対象から外す。
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// 親いっぱいに広げる。以降はアンカーを動かすだけで、親に対する範囲（0〜1）を指定できる。
    /// </summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);
    }
}
