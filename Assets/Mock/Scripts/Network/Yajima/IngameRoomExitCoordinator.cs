using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima
{
    /// <summary>既存ルーム管理を変更せず、ゲーム中の退出にシーン移動と所有物の破棄を連携する。</summary>
    [DisallowMultipleComponent]
    public sealed class IngameRoomExitCoordinator : MonoBehaviour
    {
        public const string IngamePath = "Assets/Mock/Scenes/Net/Yajima/IngameTest.unity";
        private NetworkManager _network;
        private RoomManager _rooms;
        private readonly HashSet<int> _pending = new();
        private bool _returningToBrowser;
        private static readonly byte[] ExitSceneTag = { 89, 65, 74, 73, 77, 65, 45, 69, 88, 73, 84 };

        private void Awake()
        {
            // 接続を部屋単位で管理する構成では、新しいライフサイクルが切断と復帰を担当する。
            if (GetComponent<RoomConnectionLifecycle>() != null) { enabled = false; return; }
            _network = GetComponent<NetworkManager>();
            _rooms = GetComponent<RoomManager>();
            if (_rooms != null) _rooms.MemberLeft += OnMemberLeft;
            _network.ClientManager.OnClientConnectionState += OnClientState;
            _network.SceneManager.OnUnloadEnd += OnUnloadEnd;
        }

        private void OnMemberLeft(int clientId)
        {
            if (!_network.ServerManager.Started ||
                !_network.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection connection)) return;
            if (!connection.Scenes.Any(scene => scene.path == IngamePath) || !_pending.Add(clientId)) return;
            StartCoroutine(ReturnToBrowser(connection));
        }

        private IEnumerator ReturnToBrowser(NetworkConnection connection)
        {
            // RoomManagerが退出通知とメンバー更新を送り終わってからシーンキューへ追加する。
            int clientId = connection.ClientId;
            yield return null;
            _pending.Remove(clientId);
            if (!_network.ServerManager.Started || !connection.IsActive) yield break;
            if (!Application.CanStreamedLevelBeLoaded(RoomSceneTransition.BrowserPath))
            {
                Debug.LogError("退出先のNetTest.unityをBuild ProfilesのScene Listに追加してください。", this);
                yield break;
            }
            foreach (var owned in connection.Objects.ToArray())
                if (owned != null && owned.IsSpawned && owned.gameObject.scene.path == IngamePath)
                    _network.ServerManager.Despawn(owned);
            // Browserをサーバーにロードしない。サーバー側のsceneLoaded/activeScene変更は
            // 退出していないホストの画面にも作用してしまう。
            // 本人のシーン所属だけを外し、クライアント側の完了通知後にローカルUIを開く。
            var scenes = connection.Scenes.Where(scene => scene.path == IngamePath).ToArray();
            if (scenes.Length == 0) yield break;
            var unload = new SceneUnloadData(scenes);
            unload.Params.ClientParams = ExitSceneTag;
            _network.SceneManager.UnloadConnectionScenes(connection, unload);
        }

        private void OnUnloadEnd(SceneUnloadEndEventArgs args)
        {
            if (args.QueueData.AsServer || _returningToBrowser) return;
            byte[] tag = args.QueueData.SceneUnloadData.Params.ClientParams;
            if (tag == null || !tag.SequenceEqual(ExitSceneTag)) return;
            _returningToBrowser = true;
            StartCoroutine(OpenBrowserLocally());
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped || _returningToBrowser) return;
            var game = UnitySceneManager.GetSceneByPath(IngamePath);
            if (!game.IsValid() || !game.isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(RoomSceneTransition.BrowserPath)) return;
            _returningToBrowser = true;
            StartCoroutine(OpenBrowserLocally());
        }

        private IEnumerator OpenBrowserLocally()
        {
            yield return null;
            var browser = UnitySceneManager.GetSceneByPath(RoomSceneTransition.BrowserPath);
            if (!browser.IsValid() || !browser.isLoaded)
                yield return UnitySceneManager.LoadSceneAsync(RoomSceneTransition.BrowserPath,
                    _network.ServerManager.Started ? LoadSceneMode.Additive : LoadSceneMode.Single);
            browser = UnitySceneManager.GetSceneByPath(RoomSceneTransition.BrowserPath);
            if (browser.IsValid() && browser.isLoaded) UnitySceneManager.SetActiveScene(browser);
            _returningToBrowser = false;
        }

        private void OnDestroy()
        {
            if (_rooms != null) _rooms.MemberLeft -= OnMemberLeft;
            if (_network != null)
            {
                _network.ClientManager.OnClientConnectionState -= OnClientState;
                _network.SceneManager.OnUnloadEnd -= OnUnloadEnd;
            }
        }
    }
}
