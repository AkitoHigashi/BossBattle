using System.Collections;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima
{
    /// <summary>接続終了後に一覧へ戻る構成専用のシーン遷移。ローカルロードの競合を防ぐ。</summary>
    public sealed class RoomConnectionSceneNavigation : MonoBehaviour
    {
        [SerializeField] private string _scenePath = "Assets/Mock/Scenes/Net/Yajima/IngameTest.unity";
        [SerializeField] private GameObject _connectionUI;
        private NetworkManager _network;
        private RoomClient _client;
        private RoomManager _rooms;
        private string _desired;
        public bool IsNavigating { get; private set; }

        private void Start()
        {
            _network = GetComponent<NetworkManager>();
            _client = GetComponent<RoomClient>();
            _rooms = GetComponent<RoomManager>();
            _client.StateChanged += RoomState;
            _rooms.RoomStarted += RoomStarted;
        }

        private void Update()
        {
            if (_connectionUI != null)
                _connectionUI.SetActive(UnitySceneManager.GetActiveScene().path == RoomSceneTransition.BrowserPath);
        }

        private void RoomState()
        {
            if (_client.HasRoom && !_client.CurrentRoom.HasStarted) Navigate(RoomSceneTransition.LobbyPath);
        }

        public void ReturnToBrowser() => Navigate(RoomSceneTransition.BrowserPath);

        private void Navigate(string path)
        {
            _desired = path;
            if (!IsNavigating) StartCoroutine(LoadDesired());
        }

        private IEnumerator LoadDesired()
        {
            IsNavigating = true;
            while (UnitySceneManager.GetActiveScene().path != _desired)
            {
                if (!Application.CanStreamedLevelBeLoaded(_desired))
                { Debug.LogError("Scene Listにシーンがありません: " + _desired, this); break; }
                yield return UnitySceneManager.LoadSceneAsync(_desired);
            }
            IsNavigating = false;
        }

        private void RoomStarted(RoomInfo room)
        {
            if (!Application.CanStreamedLevelBeLoaded(_scenePath))
            { Debug.LogError("Scene Listにゲームシーンがありません: " + _scenePath, this); return; }
            var connections = room.Members.Where(member => _network.ServerManager.Clients.ContainsKey(member.ClientId))
                .Select(member => _network.ServerManager.Clients[member.ClientId]).ToArray();
            if (connections.Length != 0)
                _network.SceneManager.LoadConnectionScenes(connections,
                    new SceneLoadData(_scenePath) { ReplaceScenes = ReplaceOption.All });
        }

        private void OnDestroy()
        {
            if (_client != null) _client.StateChanged -= RoomState;
            if (_rooms != null) _rooms.RoomStarted -= RoomStarted;
        }
    }
}
