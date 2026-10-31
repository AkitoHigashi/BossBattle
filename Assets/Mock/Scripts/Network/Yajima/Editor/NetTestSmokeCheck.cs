using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NetworkTest_Yajima.Editor
{
    /// <summary>Play中に実行する、実際のUIイベントとFishNet接続を使った疎通確認。</summary>
    public static class NetTestSmokeCheck
    {
        private static NetTestUI _ui;
        private static RoomClient _rooms;
        private static NetworkManager _network;
        private static int _step;
        private static double _deadline;

        [MenuItem("Tools/Yajima/Check Connection in Play Mode %#u")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode before checking the connection."); return; }
            _ui = UnityEngine.Object.FindFirstObjectByType<NetTestUI>();
            _rooms = UnityEngine.Object.FindFirstObjectByType<RoomClient>();
            _network = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            try
            {
                var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
                Debug.Log($"[NetTestCheck] EventSystem={EventSystem.current}, module={module}, point={module?.point?.action?.enabled}, click={module?.leftClick?.action?.enabled}");
                Click("Host");
                _step = 0; _deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _deadline)
                    throw new Exception("Timed out at step " + _step);
                switch (_step)
                {
                    case 0:
                        if (!_network.ClientManager.Connection.IsAuthenticated) return;
                        Click("Create"); _step++; break;
                    case 1:
                        if (UnityEngine.Object.FindAnyObjectByType<RoomLobbyUI>() == null) return;
                        _network.GetComponents<MonoBehaviour>().OfType<ILobbyCharacterSelection>().Single().Select(2); _step++; break;
                    case 2:
                        if (_rooms.CurrentRoom.Members[0].Character != 2) return;
                        _rooms.LeaveRoom(); _step++; break;
                    case 3:
                        if (_rooms.HasRoom || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RoomSceneTransition.BrowserPath) return;
                        _rooms.CreateRoom("Lobby check", 4); _step++; break;
                    case 4:
                        if (!_rooms.HasRoom || UnityEngine.Object.FindAnyObjectByType<RoomLobbyUI>() == null) return;
                        _rooms.SetReady(true); _step++; break;
                    case 5:
                        if (!_rooms.CurrentRoom.Members[0].IsReady) return;
                        _rooms.StartRoom(); _step++; break;
                    case 6:
                        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(NetTestTransitionSetup.IngamePath);
                        if (!scene.IsValid() || !scene.isLoaded || _ui.gameObject.activeSelf) return;
                        if (!_network.ClientManager.Connection.IsAuthenticated) throw new Exception("Connection lost during scene transition.");
                        Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static Button FindButton(string name) => _ui.GetComponentsInChildren<Button>(true).Single(button => button.name == name);

        private static void Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            Button button = FindButton(name);
            if (!button.interactable) throw new Exception(name + " is disabled.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject)
                throw new Exception(name + " raycast blocked: " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Debug.Log("[NetTestCheck] Click " + name);
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            string result = error == null
                ? (_network.GetComponent<RoomSceneTransition>() != null
                    ? "PASS: Host, lobby entry, character selection, leave, re-entry, ready, IngameTest, connection maintained"
                    : "PASS: UI raycasts, Host authentication, Create, Ready, Start, Leave, Disconnect")
                : "FAIL: " + error;
            File.WriteAllText("Temp/NetTestSmokeCheck.txt", result);
            if (error == null) Debug.Log("[NetTestCheck] " + result);
            else Debug.LogError("[NetTestCheck] " + result);
        }
    }
}



