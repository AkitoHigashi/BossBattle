using FishNet.Managing;
using UnityEngine;
using UnityEngine.UI;

namespace NetworkTest_Yajima
{
    /// <summary>既存のNetTest Canvasを利用し、独立した接続操作を部屋作成・参加へ置き換える。</summary>
    public sealed class RoomConnectionBrowserUI : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private InputField _address, _port, _roomName, _capacity;
        [SerializeField] private Button _host, _client, _server, _disconnect, _create, _refresh, _leave, _ready, _start;
        [SerializeField] private Text _connectionStatus, _message, _members, _roomTitle, _listStatus;
        private RoomConnectionLifecycle _session;
        private string _inputError;

        private void Start()
        {
            _session = _networkManager.GetComponent<RoomConnectionLifecycle>();
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 18);
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = font;
            _host.gameObject.SetActive(false);
            _server.gameObject.SetActive(false);
            _leave.gameObject.SetActive(false);
            _ready.gameObject.SetActive(false);
            _start.gameObject.SetActive(false);
            _refresh.gameObject.SetActive(false);
            SetCaption(_client, "部屋に参加");
            var joinRect = (RectTransform)_client.transform;
            joinRect.anchoredPosition = new Vector2(438, -50);
            joinRect.sizeDelta = new Vector2(344, 44);
            SetCaption(_create, "部屋を作成");
            SetCaption(_disconnect, "キャンセル");
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                if (text.name == "Title") text.text = "部屋の作成 / 参加";
                if (text.name == "Intro") text.text = "部屋作成でホスト起動。IP指定で参加・接続し、退出時に切断します。";
                if (text.name == "Footer") text.text = "IP接続テスト：1ホストにつき1部屋。ホストの解体で全員が切断されます。";
                if (text.name == "Heading" && text.transform.parent.name == "ConnectionPanel") text.text = "参加先ホストのIP / PORT";
                if (text.name == "Heading" && text.transform.parent.name == "RoomsPanel") text.text = "ホストとして部屋を作成";
            }
            _roomTitle.text = "参加するには";
            _members.text = "ホストが部屋を作成した後、\nそのPCのIPとポートを入力して\n「部屋に参加」を押してください。\n\nこのテストではIPを指定して参加します。";
            _listStatus.text = "部屋名と定員を設定し「部屋を作成」を押してください。";
            _create.onClick.AddListener(Create);
            _client.onClick.AddListener(Join);
            _disconnect.onClick.AddListener(_session.Cancel);
            _session.Changed += Refresh;
            Refresh();
        }

        private void Update() => Refresh();

        private void Create()
        {
            _inputError = null;
            if (!TryPort(out ushort port)) return;
            if (!int.TryParse(_capacity.text, out int capacity)) { _inputError = "定員は1〜16の整数で指定してください。"; return; }
            _session.CreateAndHost(_roomName.text, capacity, port);
        }

        private void Join()
        {
            _inputError = null;
            if (TryPort(out ushort port)) _session.JoinHost(_address.text, port);
        }

        private bool TryPort(out ushort port)
        {
            if (ushort.TryParse(_port.text, out port) && port != 0) return true;
            _inputError = "ポートは1〜65535の整数で指定してください。";
            return false;
        }

        private void Refresh()
        {
            if (_session == null) return;
            bool idle = _session.IsIdle;
            _create.interactable = _client.interactable = idle;
            _address.interactable = _port.interactable = _roomName.interactable = _capacity.interactable = idle;
            _disconnect.interactable = !idle && _session.Phase != RoomConnectionLifecycle.ConnectionPhase.Stopping;
            _connectionStatus.text = $"SERVER: {(_networkManager.ServerManager.Started ? "RUNNING" : "STOPPED")}   CLIENT: {(_networkManager.ClientManager.Started ? "CONNECTED" : "STOPPED")}   {_session.Phase}";
            _message.text = _inputError ?? _session.Status;
        }

        private static void SetCaption(Button button, string caption)
        {
            var text = button.GetComponentInChildren<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 18);
            text.text = caption;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4, 2);
            text.rectTransform.offsetMax = new Vector2(-4, -2);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Changed -= Refresh;
        }
    }
}
