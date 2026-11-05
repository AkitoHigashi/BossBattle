using System;
using System.IO;
using System.Linq;
using FishNet.Managing;
using NetworkTest_Yajima;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NCG.Character.Selection.Editor
{
    /// <summary>実接続で選択→サーバー停止→再起動→Manager破棄を確認する。</summary>
    [InitializeOnLoad]
    public static class CharacterSelectionDictionaryDebugCheck
    {
        private const string Key = "Yajima.DictionaryDebugCheck";
        private static int _step;
        private static double _deadline, _after;
        private static NetworkManager _network;
        private static RoomClient _rooms;

        static CharacterSelectionDictionaryDebugCheck()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.delayCall += () =>
            {
                if (Application.dataPath.Replace('\\', '/').Contains("/Library/VP/")) return;
                if (!File.Exists("Temp/DictionaryDebugCheck.launch")) return;
                File.Delete("Temp/DictionaryDebugCheck.launch");
                Run();
            };
        }

        [MenuItem("Tools/Yajima/Check Character Dictionary Monitor")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RoomSceneTransition.BrowserPath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            _step = 0;
            _after = 0;
            _deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        }

        private static string Display()
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<CharacterSelectionDictionaryDebugUI>();
            if (ui == null) throw new Exception("Monitor disappeared");
            return ui.GetComponentsInChildren<Text>().Single(t => t.name == "ActualDictionary").text;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _deadline)
                    throw new Exception("Timeout at step " + _step);
                if (EditorApplication.timeSinceStartup < _after) return;
                switch (_step)
                {
                    case 0:
                        _network = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
                        if (_network == null) return;
                        _rooms = _network.GetComponent<RoomClient>();
                        if (!Display().Contains("_selections.Count: 0")) return;
                        _network.ServerManager.StartConnection(18534); _step++; break;
                    case 1:
                        if (!_network.ServerManager.Started) return;
                        var selection = _network.GetComponent<CharacterSelectionManager>();
                        var settings = new SerializedObject(selection);
                        int originalDefault = settings.FindProperty("_defaultCharacterType").enumValueIndex;
                        settings.FindProperty("_defaultCharacterType").enumValueIndex = 3;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        selection.JoinRoom(9999);
                        if (!selection.TryGetCharacterType(9999, out var initial) || initial != CharacterType.CharacterD)
                            throw new Exception("Server-only JoinRoom incorrectly requires local client authentication");
                        settings.FindProperty("_defaultCharacterType").enumValueIndex = 0;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        selection.JoinRoom(9999);
                        if (!selection.TryGetCharacterType(9999, out var repeated) || repeated != CharacterType.CharacterD)
                            throw new Exception("Duplicate JoinRoom overwrote existing selection");
                        selection.LeaveRoom(9999);
                        if (selection.TryGetCharacterType(9999, out _)) throw new Exception("Server-only LeaveRoom did not remove entry");
                        settings.FindProperty("_defaultCharacterType").enumValueIndex = originalDefault;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        _network.ClientManager.StartConnection("127.0.0.1", 18534); _step++; break;
                    case 2:
                        if (!_network.ClientManager.Connection.IsAuthenticated) return;
                        _rooms.CreateRoom("Dictionary monitor check", 4); _step++; break;
                    case 3:
                        if (!_rooms.HasRoom || UnityEngine.Object.FindAnyObjectByType<RoomLobbyUI>() == null) return;
                        _network.GetComponent<CharacterSelectionManager>().SelectCharacter(CharacterType.CharacterD);
                        _step++; break;
                    case 4:
                        if (!Display().Contains("_selections.Count: 1") || !Display().Contains("CharacterD (3)")) return;
                        Canvas.ForceUpdateCanvases();
                        ScreenCapture.CaptureScreenshot("Temp/DictionaryDebug.Populated.png");
                        _after = EditorApplication.timeSinceStartup + 0.5;
                        _step++; break;
                    case 5:
                        _network.ServerManager.StopConnection(true);
                        _step++; break;
                    case 6:
                        if (_network.ServerManager.Started || _network.ClientManager.Started) return;
                        if (!Display().Contains("Server: STOPPED") || !Display().Contains("_selections.Count: 0")) return;
                        if (!Display().Contains("EMPTY")) throw new Exception("Empty state not shown");
                        Canvas.ForceUpdateCanvases();
                        ScreenCapture.CaptureScreenshot("Temp/DictionaryDebug.Cleared.png");
                        _after = EditorApplication.timeSinceStartup + 0.5;
                        _step++; break;
                    case 7:
                        _network.ServerManager.StartConnection(18534); _step++; break;
                    case 8:
                        if (!_network.ServerManager.Started) return;
                        if (!Display().Contains("_selections.Count: 0")) throw new Exception("Restart retained entries");
                        _network.ServerManager.StopConnection(true);
                        _step++; break;
                    case 9:
                        if (_network.ServerManager.Started) return;
                        UnityEngine.Object.Destroy(_network.gameObject);
                        _after = EditorApplication.timeSinceStartup + 0.5;
                        _step++; break;
                    case 10:
                        if (!Display().Contains("対象Managerなし")) return;
                        Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            string result = error == null
                ? "PASS: server-only join/leave works without local authentication; duplicate join preserves choice; actual dictionary clears on stop; monitor survives disconnect and manager destruction; restart empty"
                : "FAIL: " + error;
            File.WriteAllText("Temp/DictionaryDebugCheck.result", result);
            Debug.Log("[DictionaryDebugCheck] " + result);
            EditorApplication.ExitPlaymode();
        }
    }
}
