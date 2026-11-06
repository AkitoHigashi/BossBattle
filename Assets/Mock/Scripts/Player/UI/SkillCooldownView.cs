using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 画面下にスキルのスロットを並べ、クールダウン・使用中・Shift 押下中を表示する。
/// </summary>
/// <remarks>
/// 読む順番：
/// 1. Initialize … GameInitializer から呼ばれ、BuildUI で UI を組み立てる（起動時に1回）
/// 2. LateUpdate … 毎フレーム、UpdateShiftHighlight と UpdateSlot で見た目を更新する
///
/// 表示するだけで、クールダウンの管理やスキルとキーの対応は持たない（SkillSystem / PlayerController から読む）。
/// </remarks>
[DisallowMultipleComponent]
public class SkillCooldownView : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private Vector2 _slotSize = new Vector2(84f, 84f);
    [SerializeField, Min(0f)] private float _spacing = 8f;
    [Tooltip("画面下端からの距離（ピクセル）")]
    [SerializeField, Min(0f)] private float _bottomMargin = 24f;

    [Header("色")]
    [SerializeField] private Color _readyColor = new Color(0.15f, 0.45f, 0.25f, 0.85f);
    [SerializeField] private Color _coolingColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Color _activeColor = new Color(0.85f, 0.6f, 0.1f, 0.95f);
    [Tooltip("クールダウンの残りを表す覆い。時間とともに下へ縮む")]
    [SerializeField] private Color _overlayColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color _textColor = Color.white;

    [Header("Shift 押下中")]
    [Tooltip("Shift 付きスキルを拡大する倍率")]
    [SerializeField, Min(1f)] private float _shiftHighlightScale = 1.12f;
    [Tooltip("Shift なしスキルの不透明度")]
    [SerializeField, Range(0f, 1f)] private float _shiftDimmedAlpha = 0.3f;
    [SerializeField] private Color _shiftBannerColor = new Color(0.2f, 0.55f, 1f, 0.9f);

    // スロット1つ分の、毎フレーム書き換える UI 部品。
    private sealed class Slot
    {
        public SkillDefinition Skill;
        public bool UsesShift;
        public RectTransform Root;
        public CanvasGroup Group;
        public Image Background;
        public RectTransform Overlay;
        public Text RemainingText;
    }

    private SkillSystem _skillSystem;
    private Slot[] _slots = Array.Empty<Slot>();
    private GameObject _canvasObject;
    private GameObject _shiftBanner;
    private bool? _wasShiftHeld;
    private Font _font;

    /// <summary>
    /// 表示に使う SkillSystem と、左から並べるスキル（PlayerController.GetSkillSlots）を受け取り、UI を組み立てる。
    /// </summary>
    public void Initialize(SkillSystem skillSystem, SkillSlot[] slots)
    {
        _skillSystem = skillSystem ?? throw new ArgumentNullException(nameof(skillSystem));
        if (slots == null)
            throw new ArgumentNullException(nameof(slots));

        if (_canvasObject == null)
            BuildUI(slots);
    }

    private void LateUpdate()
    {
        if (_skillSystem == null)
            return;

        UpdateShiftHighlight();

        SkillDefinition activeSkill = _skillSystem.CurrentSkill;
        foreach (Slot slot in _slots)
            UpdateSlot(slot, slot.Skill == activeSkill);
    }

    /// <summary>
    /// 1スロット分の表示を「使用中」「クールダウン中」「使用可能」のどれかにする。
    /// </summary>
    private void UpdateSlot(Slot slot, bool isActive)
    {
        // 終了時にクールダウンが始まるスキルは、使用中はまだ残り時間が 0 なので、別に「使用中」を出す。
        if (isActive)
        {
            slot.Background.color = _activeColor;
            slot.Overlay.anchorMax = new Vector2(1f, 0f);
            slot.RemainingText.text = "IN USE";
            return;
        }

        float remaining = _skillSystem.GetCooldownRemaing(slot.Skill);
        float cooldown = slot.Skill.Cooldown;
        bool isCooling = remaining > 0f && cooldown > 0f;

        slot.Background.color = isCooling ? _coolingColor : _readyColor;
        slot.Overlay.anchorMax = new Vector2(1f, isCooling ? Mathf.Clamp01(remaining / cooldown) : 0f);

        // 1秒以上は切り上げる。切り捨てると「0」と表示されているのに使えない時間ができる。
        slot.RemainingText.text = !isCooling ? string.Empty
            : remaining < 1f ? remaining.ToString("0.0")
            : Mathf.CeilToInt(remaining).ToString();
    }

    /// <summary>
    /// Shift 押下中は帯を出し、Shift 付きスキルを拡大・それ以外を暗くする。
    /// 同じキーに Shift の有無で別のスキルがあるため、「今押すとどれが出るか」を示す。
    /// </summary>
    private void UpdateShiftHighlight()
    {
        bool isShiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        if (_wasShiftHeld == isShiftHeld)
            return;
        _wasShiftHeld = isShiftHeld;

        _shiftBanner.SetActive(isShiftHeld);
        foreach (Slot slot in _slots)
        {
            bool highlighted = isShiftHeld && slot.UsesShift;
            bool dimmed = isShiftHeld && !slot.UsesShift;
            slot.Root.localScale = Vector3.one * (highlighted ? _shiftHighlightScale : 1f);
            slot.Group.alpha = dimmed ? _shiftDimmedAlpha : 1f;
        }
    }

    // ───────────── UI の組み立て（起動時に1回） ─────────────
    // 仮の見た目なので Prefab を使わずコードで作っている。本番の見た目は Prefab に差し替える想定。

    private void BuildUI(SkillSlot[] skillSlots)
    {
        // 組み込みフォントには日本語の字形がない場合があるため、表示は英数字のみにしている。
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _canvasObject = new GameObject("SkillCooldownCanvas", typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // 溜めゲージ（100）より奥

        int count = skillSlots.Length;
        RectTransform bar = CreateRect("CooldownBar", _canvasObject.transform);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f); // 画面下中央
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, _bottomMargin);
        bar.sizeDelta = new Vector2(count * _slotSize.x + Mathf.Max(0, count - 1) * _spacing, _slotSize.y);

        _slots = new Slot[count];
        for (int index = 0; index < count; index++)
            _slots[index] = CreateSlot(bar, index, skillSlots[index]);

        _shiftBanner = CreateShiftBanner(bar);
    }

    private Slot CreateSlot(RectTransform parent, int index, SkillSlot entry)
    {
        RectTransform root = CreateRect(entry.Skill.SkillName, parent);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
        // 下端中央を基準にして、Shift 押下中の拡大が左右均等・上方向に広がるようにする。
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = _slotSize;
        root.anchoredPosition = new Vector2(index * (_slotSize.x + _spacing) + _slotSize.x * 0.5f, 0f);

        // スロット全体の不透明度をまとめて変えるために使う。
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        Image background = CreateImage(root, _readyColor);

        RectTransform overlay = CreateRect("Overlay", root);
        Stretch(overlay);
        overlay.anchorMax = new Vector2(1f, 0f);
        CreateImage(overlay, _overlayColor);

        // 上：スキル名、中央：残り秒数、下：入力キー
        CreateText(root, "Name", entry.Skill.SkillName, 11, TextAnchor.UpperCenter);
        Text remaining = CreateText(root, "Remaining", string.Empty, 26, TextAnchor.MiddleCenter);
        CreateText(root, "Key", entry.KeyLabel, 13, TextAnchor.LowerCenter);

        return new Slot
        {
            Skill = entry.Skill,
            UsesShift = entry.UsesShift,
            Root = root,
            Group = group,
            Background = background,
            Overlay = overlay,
            RemainingText = remaining
        };
    }

    private GameObject CreateShiftBanner(RectTransform bar)
    {
        RectTransform banner = CreateRect("ShiftBanner", bar);
        banner.anchorMin = new Vector2(0f, 1f);
        banner.anchorMax = new Vector2(1f, 1f);
        banner.pivot = new Vector2(0.5f, 0f);
        // 拡大したスロットと重ならない高さに置く。
        banner.anchoredPosition = new Vector2(0f, _slotSize.y * (_shiftHighlightScale - 1f) + 6f);
        banner.sizeDelta = new Vector2(0f, 26f);
        CreateImage(banner, _shiftBannerColor);
        CreateText(banner, "Label", "SHIFT", 18, TextAnchor.MiddleCenter);

        banner.gameObject.SetActive(false);
        return banner.gameObject;
    }

    // ───────────── 部品を作る補助メソッド ─────────────

    private Text CreateText(RectTransform parent, string name, string value, int fontSize, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);

        var text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = _textColor;
        // 長いスキル名が枠からはみ出さないよう縮小する。
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
        image.raycastTarget = false;
        return image;
    }

    // 親いっぱいに広げる。以降はアンカーで親に対する範囲（0〜1）を指定できる。
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
