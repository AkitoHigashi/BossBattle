using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;

namespace NetworkTest_Yajima
{
    /// <summary>ルーム開始時のシーン移動だけを担当。同期テスト用のオブジェクトは追加しません。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(RoomManager))]
    public sealed class RoomSceneTransition : MonoBehaviour
    {
        [SerializeField] private string _scenePath = "Assets/Mock/Scenes/Net/Yajima/IngameTest.unity";
        [SerializeField] private GameObject _connectionUI;
        private NetworkManager _network;
        private RoomManager _rooms;

        private void Start()
        {
            _network = GetComponent<NetworkManager>();
            _rooms = GetComponent<RoomManager>();
            _rooms.RoomStarted += OnRoomStarted;
            _network.SceneManager.OnLoadEnd += OnLoadEnd;
            _network.ClientManager.OnClientConnectionState += OnClientState;
        }

        private void OnRoomStarted(RoomInfo room)
        {
            if (!Application.CanStreamedLevelBeLoaded(_scenePath))
            {
                Debug.LogError($"移動先シーンをBuild ProfilesのScene Listに追加してください: {_scenePath}", this);
                return;
            }
            var connections = new List<NetworkConnection>();
            foreach (RoomMember member in room.Members)
                if (_network.ServerManager.Clients.TryGetValue(member.ClientId, out NetworkConnection connection))
                    connections.Add(connection);
            if (connections.Count == 0) return;
            // サーバーと対象ルームの参加者をFishNet経由で移動し、接続は維持します。
            var data = new SceneLoadData(_scenePath) { ReplaceScenes = ReplaceOption.All };
            _network.SceneManager.LoadConnectionScenes(connections.ToArray(), data);
        }

        private void OnLoadEnd(SceneLoadEndEventArgs args)
        {
            if (args.QueueData.AsServer || _connectionUI == null) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(_scenePath);
            if (scene.IsValid() && scene.isLoaded) _connectionUI.SetActive(false);
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && _connectionUI != null)
                _connectionUI.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_rooms != null) _rooms.RoomStarted -= OnRoomStarted;
            if (_network == null || !_network.Initialized) return;
            _network.SceneManager.OnLoadEnd -= OnLoadEnd;
            _network.ClientManager.OnClientConnectionState -= OnClientState;
        }
    }
}
