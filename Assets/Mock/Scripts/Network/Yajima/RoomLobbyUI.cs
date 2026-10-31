using System.Linq;
using FishNet;
using UnityEngine;
using UnityEngine.UI;
namespace NetworkTest_Yajima
{
    public sealed class RoomLobbyUI : MonoBehaviour
    {
        private RoomClient _rooms;
        private ILobbyCharacterSelection _selection;
        private System.Collections.Generic.IReadOnlyDictionary<int, string> _selected = new System.Collections.Generic.Dictionary<int, string>();
        private void ReceiveSelections(System.Collections.Generic.IReadOnlyDictionary<int, string> selections) { _selected = selections; Refresh(); }
        private string SelectionName(int id) => _selected.TryGetValue(id, out string name) ? name : "Not selected";
        private Text _title, _members, _status;
        private Button _ready, _start, _leave;
        private bool _isReady;
        private void Start()
        {
            _rooms = InstanceFinder.NetworkManager.GetComponent<RoomClient>();
            _selection = InstanceFinder.NetworkManager.GetComponents<MonoBehaviour>().OfType<ILobbyCharacterSelection>().FirstOrDefault();
            if (_selection == null) { Debug.LogError("Character selection bridge is missing", this); enabled = false; return; }
            _selection.Changed += ReceiveSelections;
            _title = FindText("RoomTitle"); _members = FindText("Members"); _status = FindText("Status");
            _ready = FindButton("Ready"); _start = FindButton("StartGame"); _leave = FindButton("LeaveRoom");
            _ready.onClick.AddListener(() => _rooms.SetReady(!_isReady));
            _start.onClick.AddListener(_rooms.StartRoom);
            _leave.onClick.AddListener(_rooms.LeaveRoom);
            // 既存のA/B/CボタンにDを加え、4種類を同じ幅で配置する。
            var template = FindButton("Character2");
            if (!GetComponentsInChildren<Button>(true).Any(b => b.name == "Character3"))
            {
                var fourth = Instantiate(template, template.transform.parent);
                fourth.name = "Character3";
                fourth.GetComponentInChildren<Text>().text = "CHARACTER D";
            }
            for (int i = 0; i < 4; i++)
            {
                int selected = i;
                var button = FindButton("Character" + i);
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(24 + i * 144, -80);
                rect.sizeDelta = new Vector2(136, 54);
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.sizeDelta = new Vector2(120, 54);
                label.fontSize = 14;
                button.onClick.AddListener(() => _selection.Select(selected));
            }
            _rooms.StateChanged += Refresh;
            _rooms.RequestCompleted += Reply;
            Refresh();
            _selection.RequestList();
        }
        private Text FindText(string name) => GetComponentsInChildren<Text>(true).First(t => t.name == name);
        private Button FindButton(string name) => GetComponentsInChildren<Button>(true).First(t => t.name == name);
        private void Reply(RoomReply reply) { if (!reply.Success) _status.text = reply.Error; }
        private void Refresh()
        {
            bool valid = _rooms.HasRoom;
            _ready.interactable = _leave.interactable = valid && !_rooms.CurrentRoom.HasStarted;
            _start.interactable = false;
            if (!valid) { _status.text = "Returning to room browser..."; return; }
            var room = _rooms.CurrentRoom;
            int self = InstanceFinder.ClientManager.Connection.ClientId;
            _title.text = room.Name + "   /   " + room.Members.Length + " / " + room.Capacity;
            _members.text = string.Join("\n\n", room.Members.Select(m => $"Client {m.ClientId}{(m.ClientId == self ? "  (YOU)" : "")}{(m.ClientId == room.OwnerId ? "  [OWNER]" : "")}\n{SelectionName(m.ClientId)}     |     {(m.IsReady ? "READY" : "WAITING")}"));
            var mine = room.Members.FirstOrDefault(m => m.ClientId == self);
            _isReady = mine.IsReady;
            _ready.GetComponentInChildren<Text>().text = _isReady ? "CANCEL READY" : "READY";
            bool allReady = room.Members.Length > 0 && room.Members.All(m => m.IsReady);
            _start.interactable = self == room.OwnerId && allReady && !room.HasStarted;
            _status.text = room.HasStarted ? "Loading IngameTest..." : allReady ? "Everyone is ready. Room owner can start." : "Choose a character, then press READY.";
            FindText("Selection").text = "YOUR SELECTION: " + SelectionName(self);
            for (int i = 0; i < 4; i++)
                FindButton("Character" + i).interactable = !room.HasStarted
                    && SelectionName(self) != "Character" + (char)('A' + i);
        }
        private void OnDestroy()
        {
            if (_selection != null) _selection.Changed -= ReceiveSelections;
            if (_rooms == null) return;
            _rooms.StateChanged -= Refresh; _rooms.RequestCompleted -= Reply;
        }
    }
}
