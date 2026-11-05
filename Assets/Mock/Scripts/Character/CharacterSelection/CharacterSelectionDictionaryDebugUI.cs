using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NCG.Character.Selection
{
    /// <summary>
    /// ローカルプロセス内の実辞書を読み取る常設モニター。通信・選択・クリア処理は行わない。
    /// 非公開フィールドへのアクセスはこの読み取り専用アダプターに限定する。
    /// </summary>
    public sealed class CharacterSelectionDictionaryDebugUI : MonoBehaviour
    {
        private static CharacterSelectionDictionaryDebugUI _instance;
        private static readonly FieldInfo SelectionsField = typeof(CharacterSelectionManager)
            .GetField("_selections", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<string, Observation> _previous = new();
        private readonly Queue<string> _history = new();
        private Text _contents, _historyText;
        private RectTransform _panel, _canvasRect;
        private Canvas _canvas;
        private Font _font;
        private float _nextPoll;

        private struct Observation
        {
            public string Fingerprint;
            public int Count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            _instance = FindAnyObjectByType<CharacterSelectionDictionaryDebugUI>();
            if (_instance != null) return;
            new GameObject("CharacterSelectionDictionaryDebugUI").AddComponent<CharacterSelectionDictionaryDebugUI>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            Poll();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.2f;
            Poll();
        }

        private void Poll()
        {
            if (_contents == null) return;
            if (SelectionsField == null)
            {
                _contents.text = "読み取りエラー：_selections が見つかりません。\n空の辞書とは判定していません。";
                return;
            }
            var managers = FindObjectsByType<CharacterSelectionManager>(FindObjectsInactive.Include)
                .OrderBy(manager => manager.GetEntityId().ToString()).ToArray();
            var seen = new HashSet<string>();
            var text = new StringBuilder();
            foreach (var manager in managers)
            {
                string id = manager.GetEntityId().ToString();
                seen.Add(id);
                string name = manager.gameObject.name + " #" + id;
                NetworkManager network = manager.GetComponent<NetworkManager>();
                if (network == null) network = FishNet.InstanceFinder.NetworkManager;
                bool server = network != null && network.ServerManager != null && network.ServerManager.Started;
                bool client = network != null && network.ClientManager != null && network.ClientManager.Started;
                text.AppendLine(name);
                text.AppendLine($"Server: {(server ? "RUNNING" : "STOPPED")} / Client: {(client ? "CONNECTED" : "STOPPED")}");
                if (client && !server) text.AppendLine("クライアントのローカル辞書（サーバー実体ではありません）");
                if (!manager.isActiveAndEnabled) text.AppendLine("Manager: DISABLED（保持データを表示）");
                try
                {
                    var dictionary = SelectionsField.GetValue(manager) as Dictionary<int, CharacterType>;
                    if (dictionary == null)
                    {
                        text.AppendLine("読み取りエラー：辞書を取得できません。\n");
                        continue;
                    }
                    // 接続中のID一覧で絞り込まない。切断済みIDの残留もそのまま見せる。
                    var entries = dictionary.OrderBy(entry => entry.Key).ToArray();
                    text.AppendLine($"_selections.Count: {entries.Length}");
                    if (entries.Length == 0) text.AppendLine("EMPTY / 辞書は空です");
                    var fingerprint = new StringBuilder();
                    foreach (var entry in entries)
                    {
                        text.AppendLine($"[{entry.Key}] → {entry.Value} ({(int)entry.Value})");
                        fingerprint.Append(entry.Key).Append(':').Append((int)entry.Value).Append(';');
                    }
                    string contents = fingerprint.ToString();
                    if (!_previous.TryGetValue(id, out Observation previous))
                        Record($"#{id}: 検出 / Count {entries.Length}");
                    else if (previous.Fingerprint != contents)
                        Record($"#{id}: Count {previous.Count} → {entries.Length}" +
                            (previous.Count == entries.Length ? " / 値変更" : ""));
                    _previous[id] = new Observation { Count = entries.Length, Fingerprint = contents };
                }
                catch (Exception exception)
                {
                    text.AppendLine("読み取りエラー：" + exception.GetType().Name);
                }
                text.AppendLine();
            }
            foreach (string id in _previous.Keys.Where(id => !seen.Contains(id)).ToArray())
            {
                Observation last = _previous[id];
                Record($"#{id}: 破棄 / 最終観測Count {last.Count}");
                _previous.Remove(id);
            }
            if (managers.Length == 0)
                text.AppendLine("対象Managerなし（未生成または破棄済み）\n辞書のクリアを確認した状態とは異なります。");
            _contents.text = text.ToString();
            _historyText.text = "変更履歴（直近6件）\n" + string.Join("\n", _history);
        }

        private void Record(string value)
        {
            _history.Enqueue($"{DateTime.Now:HH:mm:ss} {value}");
            while (_history.Count > 6) _history.Dequeue();
        }

        private void BuildUI()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 18);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("DictionaryDebugCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            _canvasRect = root.GetComponent<RectTransform>();
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            _panel = MakeRect("DictionaryDebugPanel", root.transform, 16, 16, 460, 390);
            _panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.97f);
            RectTransform header = MakeRect("DragHeader", _panel, 0, 0, 460, 38);
            header.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.25f, 0.32f);
            Label(header, "Title", "CharacterSelectionManager / 実辞書", 12, 5, 436, 28, 18);
            var drag = header.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(data =>
            {
                Vector2 position = _panel.anchoredPosition + ((PointerEventData)data).delta / _canvas.scaleFactor;
                position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, _canvasRect.rect.width - _panel.rect.width));
                position.y = Mathf.Clamp(position.y, Mathf.Min(0, _panel.rect.height - _canvasRect.rect.height), 0);
                _panel.anchoredPosition = position;
            });
            drag.triggers.Add(entry);
            Label(_panel, "Hint", "見出しをドラッグで移動 / 0.2秒ごとに読取 / 書込なし", 12, 44, 436, 24, 14);
            RectTransform viewport = MakeRect("DictionaryViewport", _panel, 12, 74, 436, 168);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _contents = Label(viewport, "ActualDictionary", "取得中…", 8, 4, 418, 160, 16);
            _contents.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _contents.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            _historyText = Label(_panel, "DictionaryHistory", "", 12, 253, 436, 128, 13);
        }

        private static RectTransform MakeRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            var text = MakeRect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = new Color(0.88f, 0.95f, 1f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
