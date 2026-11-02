using System;
using System.IO;
using System.Linq;
using FishNet.Managing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NetworkTest_Yajima.Editor
{
    /// <summary>既存クラスを変更せず、Host→選択→ゲームUI→退出を実際の接続で確認する。</summary>
    [InitializeOnLoad]
    public static class IngameRoomUISmokeCheck
    {
        private const string Key = "Yajima.IngameRoomUICheck";
        private static int _step;
        private static double _deadline;
        private static double _nextStepAt;
        private static NetworkManager _network;
        private static RoomClient _rooms;
        private static IngameRoomUI _ui;

        static IngameRoomUISmokeCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Yajima/Check Ingame Room UI")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before running this check.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RoomSceneTransition.BrowserPath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            _step = 0;
            _nextStepAt = 0;
            _deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                    throw new Exception("Timeout at step " + _step);
                if (EditorApplication.timeSinceStartup < _nextStepAt) return;
                int previousStep = _step;
                switch (_step)
                {
                    case 0:
                        _network = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
                        if (_network == null || _network.GetComponent<RoomClient>() == null) return;
                        _rooms = _network.GetComponent<RoomClient>();
                        if (!_network.ServerManager.StartConnection(18532)) throw new Exception("Server start failed");
                        _step++; break;
                    case 1:
                        if (!_network.ServerManager.Started) return;
                        _network.ClientManager.StartConnection("127.0.0.1", 18532);
                        _step++; break;
                    case 2:
                        if (!_network.ClientManager.Connection.IsAuthenticated) return;
                        _rooms.CreateRoom("Ingame UI check", 4);
                        _step++; break;
                    case 3:
                        if (!_rooms.HasRoom || UnityEngine.Object.FindAnyObjectByType<RoomLobbyUI>() == null) return;
                        _network.GetComponents<MonoBehaviour>().OfType<ILobbyCharacterSelection>().Single().Select(3);
                        _step++; break;
                    case 4:
                        if (_rooms.CurrentRoom.Members[0].Character != 3) return;
                        _rooms.SetReady(true);
                        _step++; break;
                    case 5:
                        if (!_rooms.CurrentRoom.Members[0].IsReady) return;
                        _rooms.StartRoom();
                        _step++; break;
                    case 6:
                        _ui = UnityEngine.Object.FindAnyObjectByType<IngameRoomUI>();
                        if (_ui == null) return;
                        var members = _ui.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Members");
                        if (members == null || !members.text.Contains("キャラクター D") || !members.text.Contains("あなた")) return;
                        Canvas.ForceUpdateCanvases();
                        ScreenCapture.CaptureScreenshot("Temp/IngameRoomUI.png");
                        _step++; break;
                    case 7:
                        Click("LeaveRoom");
                        _step++; break;
                    case 8:
                        if (!FindButton("Cancel").gameObject.activeInHierarchy) throw new Exception("Confirmation missing");
                        Click("Cancel");
                        if (!FindButton("LeaveRoom").interactable) throw new Exception("Cancel did not restore UI");
                        _step++; break;
                    case 9:
                        Click("LeaveRoom");
                        _step++; break;
                    case 10:
                        Click("ConfirmLeave");
                        _step++; break;
                    case 11:
                        if (_rooms.HasRoom || SceneManager.GetActiveScene().path != RoomSceneTransition.BrowserPath) return;
                        if (!_network.ClientManager.Connection.IsAuthenticated || !_network.ServerManager.Started)
                            throw new Exception("Host connection/server was stopped");
                        if (_network.ClientManager.Connection.Objects.Any(o => o != null && o.gameObject.scene.path == IngameRoomExitCoordinator.IngamePath))
                            throw new Exception("Owned player left in game scene");
                        Finish(null); break;
                }
                if (_step != previousStep) _nextStepAt = EditorApplication.timeSinceStartup + 0.25;
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static Button FindButton(string name) => _ui.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

        private static void Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            var button = FindButton(name);
            if (!button.interactable || !button.gameObject.activeInHierarchy) throw new Exception(name + " not available");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject)
                throw new Exception(name + " raycast blocked: " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            string result = error == null
                ? "PASS: Host, Character D, member display, cancel, leave confirmation, browser return, connection/server maintained, player cleanup"
                : "FAIL: " + error;
            File.WriteAllText("Temp/IngameRoomUISmokeCheck.txt", result);
            Debug.Log("[IngameRoomUICheck] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
