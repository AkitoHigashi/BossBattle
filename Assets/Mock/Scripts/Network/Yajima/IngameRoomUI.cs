using System.Collections.Generic;
using System.Linq;
using System.Text;
using FishNet;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NetworkTest_Yajima
{
    /// <summary>IngameTestの参加者・キャラクター表示。既存クラスは公開APIとイベントだけを利用する。</summary>
    public sealed class IngameRoomUI : MonoBehaviour
    {
        private NetworkManager _network;
        private RoomClient _rooms;
        private ILobbyCharacterSelection _selection;
        private IReadOnlyDictionary<int, string> _characters = new Dictionary<int, string>();
        private Font _font;
        private Text _title, _members, _status;
        private Button _leave, _refresh;
        private GameObject _confirmation;
        private Canvas _canvas;
        private bool _leaving;
        private float _leaveStarted;

        private void Start()
        {
            _network = InstanceFinder.NetworkManager;
            BuildUI();
            if (_network == null)
            {
                _status.text = "接続がありません。NetTestから部屋に参加してください。";
                _leave.interactable = _refresh.interactable = false;
                return;
            }
            if (_network.GetComponent<IngameRoomExitCoordinator>() == null)
                _network.gameObject.AddComponent<IngameRoomExitCoordinator>();
            _rooms = _network.GetComponent<RoomClient>();
            _selection = _network.GetComponents<MonoBehaviour>().OfType<ILobbyCharacterSelection>().FirstOrDefault();
            if (_rooms == null)
            {
                _status.text = "RoomClientが見つかりません。";
                _leave.interactable = _refresh.interactable = false;
                return;
            }
            _rooms.StateChanged += Refresh;
            _rooms.RequestCompleted += OnReply;
            if (_selection != null) _selection.Changed += OnSelections;
            _leave.onClick.AddListener(() => _confirmation.SetActive(true));
            _refresh.onClick.AddListener(RequestRefresh);
            Refresh();
            RequestRefresh();
        }

        private void Update()
        {
            // ホストが退出してもサーバー用に残っているゲームシーンのUIは表示しない。
            if (_network != null && _canvas != null)
                _canvas.enabled = _network.ClientManager.Started &&
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == IngameRoomExitCoordinator.IngamePath;
            if (_leaving && Time.unscaledTime - _leaveStarted > 15f && _rooms != null && _rooms.HasRoom)
            {
                _leaving = false;
                Refresh();
                _status.text = "退出応答を確認できません。接続を確認して再度お試しください。";
            }
        }

        private void RequestRefresh()
        {
            _selection?.RequestList();
            Refresh();
        }

        private void OnSelections(IReadOnlyDictionary<int, string> characters)
        {
            _characters = characters;
            Refresh();
        }

        private void Refresh()
        {
            bool inRoom = _rooms != null && _rooms.HasRoom;
            bool connected = _network != null && _network.ClientManager.Started;
            var session = _network != null ? _network.GetComponent<RoomConnectionLifecycle>() : null;
            if (session != null)
            {
                _leave.GetComponentInChildren<Text>().text = session.IsHost ? "部屋を解体" : "部屋から退出";
                _confirmation.transform.Find("Question").GetComponent<Text>().text = session.IsHost
                    ? "解体して全員を切断しますか？" : "この部屋から退出しますか？";
            }
            _leave.interactable = inRoom && connected && !_leaving;
            _refresh.interactable = inRoom && connected && !_leaving;
            if (!inRoom)
            {
                _title.text = "ルーム情報";
                _members.text = "ルームに参加していません。";
                _status.text = _leaving ? "退出しました。部屋一覧へ移動中…" : "NetTestから部屋に参加してください。";
                return;
            }
            RoomInfo room = _rooms.CurrentRoom;
            RoomMember[] members = room.Members ?? System.Array.Empty<RoomMember>();
            _title.text = $"{room.Name}  ({members.Length}/{room.Capacity})";
            int self = _network.ClientManager.Connection.ClientId;
            var text = new StringBuilder();
            foreach (RoomMember member in members)
            {
                string character = _characters.TryGetValue(member.ClientId, out string name)
                    ? DisplayCharacter(name) : "未選択";
                text.AppendLine($"Client {member.ClientId}{(member.ClientId == self ? "  あなた" : "")}{(member.ClientId == room.OwnerId ? "  オーナー" : "")}");
                text.AppendLine($"選択キャラクター：{character}");
                text.AppendLine();
            }
            _members.text = text.ToString();
            _status.text = _leaving ? "退出を依頼しています…" :
                _network.GetComponent<RoomConnectionLifecycle>() != null
                    ? (_network.ServerManager.Started ? "ホストの解体で全員が切断されます。" : "退出するとホストとの接続を切ります。")
                    : (_network.ServerManager.Started ? "ホストは退出後もサーバーを継続します。" : "退出すると接続を維持して部屋一覧へ戻ります。");
        }

        private static string DisplayCharacter(string value)
        {
            switch (value)
            {
                case "CharacterA": return "キャラクター A";
                case "CharacterB": return "キャラクター B";
                case "CharacterC": return "キャラクター C";
                case "CharacterD": return "キャラクター D";
                default: return value;
            }
        }

        private void Leave()
        {
            _confirmation.SetActive(false);
            if (_leaving || _rooms == null || !_rooms.HasRoom) return;
            if (!Application.CanStreamedLevelBeLoaded(RoomSceneTransition.BrowserPath))
            {
                _status.text = "退出先のNetTestシーンがScene Listにありません。";
                return;
            }
            _leaving = true;
            _leaveStarted = Time.unscaledTime;
            Refresh();
            var lifecycle = _network.GetComponent<RoomConnectionLifecycle>();
            if (lifecycle != null) lifecycle.LeaveOrDissolve();
            else _rooms.LeaveRoom();
        }

        private void OnReply(RoomReply reply)
        {
            if (reply.Operation != RoomOperation.Leave || reply.Success) return;
            _leaving = false;
            Refresh();
            _status.text = reply.Error;
        }

        private void BuildUI()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 20);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("IngameRoomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform panel = Rect("RoomPanel", root.transform, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(360, 520));
            panel.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.075f, 0.12f, 0.96f);
            Label(panel, "Heading", "参加中のルーム", 18, 16, 324, 30, 24);
            _title = Label(panel, "RoomTitle", "ルーム情報", 18, 53, 324, 38, 19);
            Label(panel, "MembersHeading", "参加者 / 選択キャラクター", 18, 99, 324, 28, 17);
            RectTransform viewport = Rect("MembersScroll", panel, new Vector2(0, 1), new Vector2(18, -136), new Vector2(324, 232));
            viewport.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.13f, 0.19f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _members = Label(viewport, "Members", "参加者情報を取得中…", 10, 8, 302, 220, 18);
            var fitter = _members.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _members.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            _status = Label(panel, "Status", "", 18, 380, 324, 52, 15);
            _refresh = Button(panel, "Refresh", "表示を更新", 18, 449, 146);
            _leave = Button(panel, "LeaveRoom", "部屋から退出", 174, 449, 168);
            _leave.GetComponent<Image>().color = new Color(0.65f, 0.2f, 0.23f);
            RectTransform confirm = Rect("LeaveConfirmation", panel, new Vector2(0, 1), new Vector2(8, -370), new Vector2(344, 142));
            confirm.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f);
            Label(confirm, "Question", "この部屋から退出しますか？", 12, 12, 320, 34, 19);
            Button(confirm, "ConfirmLeave", "退出する", 12, 70, 150).onClick.AddListener(Leave);
            Button(confirm, "Cancel", "キャンセル", 182, 70, 150).onClick.AddListener(() => _confirmation.SetActive(false));
            _confirmation = confirm.gameObject;
            _confirmation.SetActive(false);
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("IngameRoomEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int fontSize)
        {
            var text = Rect(name, parent, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height)).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = new Color(0.94f, 0.96f, 1f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private Button Button(Transform parent, string name, string caption, float x, float y, float width)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, 48));
            rect.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.33f, 0.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var text = Label(rect, "Label", caption, 0, 0, width, 48, 18);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private void OnDestroy()
        {
            if (_selection != null) _selection.Changed -= OnSelections;
            if (_rooms == null) return;
            _rooms.StateChanged -= Refresh;
            _rooms.RequestCompleted -= OnReply;
        }
    }
}
