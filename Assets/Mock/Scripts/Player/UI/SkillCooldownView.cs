using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全スキルのクールダウンを、画面下に並べたスロットで表示する。
/// スロットごとに「キー」「スキル名」「残り秒数」と、残り時間に応じて縮む暗い覆いを描く。
/// </summary>
/// <remarks>
/// 残り時間は SkillSystem.GetCooldownRemaing を毎フレーム読むだけで、クールダウンの管理には関わらない。
/// ChargeGaugeView と同じく、仮の見た目なので Canvas や Image を実行時にコードで組み立てている。
///
/// 終了時開始（OnEnd）のスキルは、実行中はまだクールダウンが始まっていないため「使用可能」の表示のままになる。
/// 実行中の表示が必要になったら、SkillSystem から実行中のスキルを取得できるようにする必要がある。
/// </remarks>
[DisallowMultipleComponent]
public class SkillCooldownView : MonoBehaviour
{
    /// <summary>
    /// 表示するスキル1つ分の設定。
    /// </summary>
    [Serializable]
    private class Entry
    {
        public SkillDefinition Skill => _skill;
        public string KeyLabel => _keyLabel;

        [Tooltip("表示するスキル")]
        [SerializeField] private SkillDefinition _skill;
        [Tooltip("スロットに表示する入力キー（例：Shift+Q）")]
        [SerializeField] private string _keyLabel;
    }

    [Tooltip("左から順に並べるスキル")]
    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
    [Tooltip("スロット1つの大きさ（ピクセル）")]
    [SerializeField] private Vector2 _slotSize = new Vector2(84f, 84f);
    [Tooltip("スロット同士の間隔（ピクセル）")]
    [SerializeField, Min(0f)] private float _spacing = 8f;
    [Tooltip("画面下端からの距離（ピクセル）")]
    [SerializeField, Min(0f)] private float _bottomMargin = 24f;

    [Header("色")]
    [SerializeField] private Color _readyColor = new Color(0.15f, 0.45f, 0.25f, 0.85f);
    [SerializeField] private Color _coolingColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    [Tooltip("残り時間を表す覆い。上から下へ縮んでいき、なくなると使用可能")]
    [SerializeField] private Color _overlayColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color _textColor = Color.white;

    /// <summary>
    /// スロット1つ分の UI 部品。毎フレーム書き換える部分だけ保持する。
    /// </summary>
    private sealed class Slot
    {
        public SkillDefinition Skill;
        public Image Background;
        public RectTransform Overlay;
        public Text RemainingText;
    }

    private SkillSystem _skillSystem;
    private Slot[] _slots = Array.Empty<Slot>();
    private GameObject _canvasObject;
    private Font _font;

    /// <summary>
    /// クールダウンを読む SkillSystem を設定し、UI を組み立てる。CharacterComposition の初期化時に呼ばれる。
    /// </summary>
    public void Initialize(SkillSystem skillSystem)
    {
        _skillSystem = skillSystem ?? throw new ArgumentNullException(nameof(skillSystem));

        if (_canvasObject == null)
            BuildUI();
    }

    private void LateUpdate()
    {
        if (_skillSystem == null)
            return;

        foreach (Slot slot in _slots)
        {
            float remaining = _skillSystem.GetCooldownRemaing(slot.Skill);
            float cooldown = slot.Skill.Cooldown;
            bool isCooling = remaining > 0f && cooldown > 0f;

            slot.Background.color = isCooling ? _coolingColor : _readyColor;

            // 覆いの上端を「残り時間の割合」の高さに置く。時間が経つほど下へ縮んでいく。
            float ratio = isCooling ? Mathf.Clamp01(remaining / cooldown) : 0f;
            slot.Overlay.anchorMax = new Vector2(1f, ratio);

            // 1秒未満は小数1桁、それ以上は切り上げた整数で出す（「0」と表示されたのに使えない、を防ぐ）。
            slot.RemainingText.text = !isCooling ? string.Empty
                : remaining < 1f ? remaining.ToString("0.0")
                : Mathf.CeilToInt(remaining).ToString();
        }
    }

    /// <summary>
    /// 画面下中央に、設定された順でスロットを横に並べる。
    /// </summary>
    private void BuildUI()
    {
        // 文字の描画には Unity 組み込みのフォントを使う（フォントアセットの準備が要らない）。
        // 日本語の字形が無い場合があるため、表示する文字は英数字に限っている。
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _canvasObject = new GameObject("SkillCooldownCanvas", typeof(Canvas), typeof(CanvasScaler));
        // Screen Space Overlay の Canvas は親の移動の影響を受けないので、整理のためキャラクターの子にしておく。
        _canvasObject.transform.SetParent(transform, false);
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 溜めゲージ（100）より奥に描く。
        canvas.sortingOrder = 90;

        int count = 0;
        foreach (Entry entry in _entries)
            if (entry != null && entry.Skill != null)
                count++;

        RectTransform bar = CreateRect("CooldownBar", _canvasObject.transform);
        // 画面下中央を基準にする。
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, _bottomMargin);
        bar.sizeDelta = new Vector2(count * _slotSize.x + Mathf.Max(0, count - 1) * _spacing, _slotSize.y);

        _slots = new Slot[count];
        int index = 0;
        foreach (Entry entry in _entries)
        {
            // 未設定の行は飛ばす。Inspector で行だけ追加した状態でもエラーにしない。
            if (entry == null || entry.Skill == null)
                continue;

            _slots[index] = CreateSlot(bar, index, entry);
            index++;
        }
    }

    private Slot CreateSlot(RectTransform parent, int index, Entry entry)
    {
        RectTransform root = CreateRect(entry.Skill.SkillName, parent);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        root.sizeDelta = _slotSize;
        root.anchoredPosition = new Vector2(index * (_slotSize.x + _spacing), 0f);
        Image background = CreateImage(root, _readyColor);

        RectTransform overlay = CreateRect("Overlay", root);
        Stretch(overlay);
        overlay.anchorMax = new Vector2(1f, 0f);
        CreateImage(overlay, _overlayColor);

        // 上：スキル名（小さめ）、中央：残り秒数（大きめ）、下：入力キー
        CreateText(root, "Name", entry.Skill.SkillName, 11, TextAnchor.UpperCenter);
        Text remaining = CreateText(root, "Remaining", string.Empty, 26, TextAnchor.MiddleCenter);
        CreateText(root, "Key", entry.KeyLabel, 13, TextAnchor.LowerCenter);

        return new Slot
        {
            Skill = entry.Skill,
            Background = background,
            Overlay = overlay,
            RemainingText = remaining
        };
    }

    private Text CreateText(RectTransform parent, string name, string value, int fontSize, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect);
        // 枠の端に文字が付かないよう、少し内側に寄せる。
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);

        var text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = _textColor;
        // スキル名が長くても枠からはみ出さないよう、入りきらない分は縮小する。
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        return text;
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
}
