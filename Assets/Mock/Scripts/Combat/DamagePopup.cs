using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 命中した位置に、ダメージなどの数字を浮かび上がらせて消す表示。
/// </summary>
/// <remarks>
/// 使い方：DamagePopup.Show(ワールド座標, 表示する文字, 色) を呼ぶだけ。表示は自分で消える。
/// 今はダミー用だが、ボスなど他の被弾側からも同じように呼べる。
///
/// 仮の見た目なので Prefab を使わずコードで作っている。表示数が少ないため、プールせず毎回生成・破棄している。
/// </remarks>
public class DamagePopup : MonoBehaviour
{
    private const float LIFETIME = 0.8f;
    // 1秒あたりに上へ浮かぶ距離（ワールド単位）
    private const float RISE_SPEED = 1.5f;

    // 全ポップアップで共有する Canvas。シーンが変わると破棄されるが、Unity の null 判定で検出して作り直す。
    private static Canvas s_canvas;
    private static Font s_font;

    private Text _text;
    private Color _color;
    private Vector3 _worldPosition;
    private float _elapsed;

    /// <summary>
    /// 指定したワールド座標に文字を表示する。
    /// </summary>
    public static void Show(Vector3 worldPosition, string message, Color color, int fontSize = 28)
    {
        var gameObject = new GameObject("DamagePopup", typeof(RectTransform));
        gameObject.transform.SetParent(GetCanvas().transform, false);

        var text = gameObject.AddComponent<Text>();
        text.font = s_font;
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        // 背景の色に関係なく読めるよう、黒い縁取りを付ける。
        gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        var popup = gameObject.AddComponent<DamagePopup>();
        popup._text = text;
        popup._color = color;
        popup._worldPosition = worldPosition;
        popup.UpdatePosition();
    }

    private static Canvas GetCanvas()
    {
        if (s_canvas == null)
        {
            // 組み込みフォントには日本語の字形がない場合があるため、表示は英数字にする。
            s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("DamagePopupCanvas", typeof(Canvas), typeof(CanvasScaler));
            s_canvas = canvasObject.GetComponent<Canvas>();
            s_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            s_canvas.sortingOrder = 80; // スキル表示（90）や溜めゲージ（100）より奥
        }
        return s_canvas;
    }

    private void OnEnable() => Canvas.willRenderCanvases += UpdatePosition;
    private void OnDisable() => Canvas.willRenderCanvases -= UpdatePosition;

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= LIFETIME)
        {
            Destroy(gameObject);
            return;
        }

        _worldPosition += Vector3.up * (RISE_SPEED * Time.deltaTime);

        // 後半で透明にしていき、急に消えないようにする。
        float fade = Mathf.InverseLerp(LIFETIME, LIFETIME * 0.5f, _elapsed);
        _text.color = new Color(_color.r, _color.g, _color.b, _color.a * fade);
    }

    // 位置は UI を描く直前に合わせる。カメラ（Cinemachine）が動いた後に計算しないと、表示がカメラに遅れて揺れるため。
    private void UpdatePosition()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 screenPosition = camera.WorldToScreenPoint(_worldPosition);
        // カメラの後ろにあると画面の反対側に出てしまうので隠す。
        _text.enabled = screenPosition.z > 0f;
        transform.position = screenPosition;
    }
}
