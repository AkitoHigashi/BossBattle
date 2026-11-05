using System;
using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NetworkTest_Yajima.Editor
{
    /// <summary>メニューからUIプレハブ・NetTestシーンを再生成できます。</summary>
    public static class NetTestSceneBuilder
    {
        private const string ScenePath = "Assets/Mock/Scenes/Net/Yajima/NetTest.unity";
        private const string PrefabPath = "Assets/Mock/Prefabs/Net/Yajima/NetTestConnection.prefab";
        private static readonly Color Ink = new Color32(225, 234, 248, 255);
        private static readonly Color Muted = new Color32(148, 165, 191, 255);
        private static readonly Color Panel = new Color32(24, 36, 55, 255);
        private static Font _font;

        [MenuItem("Tools/Yajima/Rebuild NetTest UI and Scene %#y")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.GetComponentInChildren<NetworkManager>(true) != null || old.name == "NetTestConnection")
                    UnityEngine.Object.DestroyImmediate(old);
            }
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("NetTestConnection");
            var transport = root.AddComponent<Tugboat>();
            transport.SetPort(7770);
            root.AddComponent<TransportManager>().Transport = transport;
            root.AddComponent<ServerManager>().SetStartOnHeadless(false);
            var manager = root.AddComponent<NetworkManager>();
            var roomManager = root.AddComponent<RoomManager>();
            var roomClient = root.AddComponent<RoomClient>();
            Set(roomManager, "_networkManager", manager);
            Set(roomClient, "_networkManager", manager);

            var canvasObject = new GameObject("ConnectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var ui = canvasObject.AddComponent<NetTestUI>();
            Set(ui, "_networkManager", manager); Set(ui, "_rooms", roomClient);
            var background = Box("Background", canvasObject.transform, 0, 0, 1280, 800, new Color32(12, 20, 34, 255));
            // 背景のみ全画面へ追従。操作領域はCanvasScalerで均等拡縮します。
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            var layout = Rect("Layout", canvasObject.transform, 0, 0, 1280, 800);
            layout.anchorMin = layout.anchorMax = new Vector2(0.5f, 0.5f);
            layout.pivot = new Vector2(0.5f, 0.5f); layout.anchoredPosition = Vector2.zero;
            Label("Eyebrow", layout, "YAJIMA  /  FISHNET NETWORK LAB", 32, 20, 1000, 22, 14, Muted);
            Label("Title", layout, "Connection & Rooms", 32, 48, 1000, 44, 32, Ink);
            Label("Intro", layout, "Start a host on one device. Connect other clients to its LAN IP and port.", 32, 100, 1216, 24, 16, Muted);

            var connection = Box("ConnectionPanel", layout, 32, 144, 1216, 166, Panel);
            Label("Heading", connection, "01  CONNECTION", 20, 14, 500, 24, 17, Ink);
            Label("AddressLabel", connection, "SERVER IP", 20, 34, 280, 16, 11, Muted);
            Label("PortLabel", connection, "PORT", 318, 34, 100, 16, 11, Muted);
            Set(ui, "_address", Input(connection, "ServerAddress", "127.0.0.1", 20, 50, 286));
            Set(ui, "_port", Input(connection, "Port", "7770", 318, 50, 100, true));
            Set(ui, "_host", Button(connection, "Host", "HOST", 438, 50, 166, true));
            Set(ui, "_client", Button(connection, "Client", "CLIENT", 616, 50, 166));
            Set(ui, "_server", Button(connection, "Server", "SERVER ONLY", 794, 50, 166));
            Set(ui, "_disconnect", Button(connection, "Disconnect", "DISCONNECT", 972, 50, 224));
            Set(ui, "_connectionStatus", Label("ConnectionStatus", connection, "SERVER  Stopped     |     CLIENT  Stopped", 20, 108, 1176, 26, 16, Muted));

            var list = Box("RoomsPanel", layout, 32, 330, 588, 354, Panel);
            Label("Heading", list, "02  AVAILABLE ROOMS", 20, 16, 390, 24, 17, Ink);
            Set(ui, "_refresh", Button(list, "Refresh", "REFRESH", 438, 12, 130));
            Label("NameLabel", list, "ROOM NAME", 20, 46, 256, 16, 11, Muted);
            Label("CapacityLabel", list, "SEATS", 288, 46, 68, 16, 11, Muted);
            Set(ui, "_roomName", Input(list, "RoomName", "Yajima Room", 20, 64, 256));
            Set(ui, "_capacity", Input(list, "Capacity", "4", 288, 64, 68, true));
            Set(ui, "_create", Button(list, "Create", "CREATE ROOM", 368, 64, 200, true));
            Set(ui, "_listStatus", Label("ListStatus", list, "Connect to retrieve available rooms.", 20, 118, 548, 24, 14, Muted));
            var scrollRect = Rect("RoomScroll", list, 20, 156, 548, 178);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var viewport = Box("Viewport", scrollRect, 0, 0, 548, 178, new Color32(17, 27, 43, 255));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, 548, 0);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.sizeDelta = Vector2.zero;
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 8; vertical.padding = new RectOffset(0, 0, 0, 0);
            vertical.childControlHeight = true; vertical.childForceExpandHeight = false;
            vertical.childControlWidth = true; vertical.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var template = Button(content, "RoomRowTemplate", "Room name     1/4     JOIN >", 0, 0, 548);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            template.gameObject.SetActive(false);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            Set(ui, "_roomList", content); Set(ui, "_roomRowTemplate", template);

            var current = Box("CurrentRoomPanel", layout, 640, 330, 608, 354, Panel);
            Set(ui, "_roomTitle", Label("RoomTitle", current, "03  CURRENT ROOM", 20, 16, 568, 28, 17, Ink));
            Set(ui, "_members", Label("Members", current, "Join or create a room to see members here.", 20, 64, 568, 200, 16, Muted));
            Set(ui, "_leave", Button(current, "Leave", "LEAVE", 20, 288, 150));
            Set(ui, "_ready", Button(current, "Ready", "READY", 182, 288, 190));
            Set(ui, "_start", Button(current, "Start", "START ROOM", 384, 288, 204, true));
            Set(ui, "_message", Label("Message", layout, "Ready. HOST starts a local server + client. Default port: 7770.", 32, 704, 1216, 42, 16, Ink));
            Label("Footer", layout, "Owner starts when every member is ready. Room start only signals the game; no scene transition is configured.", 32, 760, 1216, 22, 13, Muted);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform, false);
            // 入力アセットも永続化し、プレハブ単体で入力参照を保持します。
            const string actionsPath = "Assets/Mock/Prefabs/Net/Yajima/NetTestUI.inputactions";
            if (!File.Exists(actionsPath))
                AssetDatabase.CopyAsset("Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions", actionsPath);
            AssetDatabase.ImportAsset(actionsPath, ImportAssetOptions.ForceSynchronousImport);
            var inputModule = events.GetComponent<InputSystemUIInputModule>();
            inputModule.UnassignActions();
            inputModule.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            var references = AssetDatabase.LoadAllAssetsAtPath(actionsPath).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name) => references.First(reference => reference.action.actionMap.name == "UI" && reference.action.name == name);
            inputModule.point = Action("Point"); inputModule.leftClick = Action("Click");
            inputModule.rightClick = Action("RightClick"); inputModule.middleClick = Action("MiddleClick");
            inputModule.move = Action("Navigate"); inputModule.submit = Action("Submit"); inputModule.cancel = Action("Cancel");
            inputModule.scrollWheel = Action("ScrollWheel");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene);
            var builds = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            builds.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
            NetTestTransitionSetup.Configure();
            Capture(canvas);
            Debug.Log("[NetTestBuilder] Created prefab and updated NetTest scene.");
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        { var rect = Rect(name, parent, x, y, w, h); rect.gameObject.AddComponent<Image>().color = color; return rect; }

        private static Text Label(string name, Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = _font; label.fontSize = size; label.color = color; label.text = text;
            label.raycastTarget = false; label.supportRichText = false;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button Button(Transform parent, string name, string title, float x, float y, float w, bool accent = false)
        {
            var rect = Box(name, parent, x, y, w, 44, accent ? new Color32(35, 106, 200, 255) : new Color32(45, 62, 87, 255));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f); button.colors = colors;
            var label = Label("Label", rect, title, 8, 0, w - 16, 44, 15, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static InputField Input(Transform parent, string name, string value, float x, float y, float w, bool integer = false)
        {
            var rect = Box(name, parent, x, y, w, 44, new Color32(12, 22, 38, 255));
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            var text = Label("Value", rect, value, 12, 0, w - 24, 44, 17, Ink);
            text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text; input.text = value;
            input.characterLimit = name == "RoomName" ? 48 : 64;
            if (integer) input.contentType = InputField.ContentType.IntegerNumber;
            return input;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }

        private static void Capture(Canvas canvas)
        {
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var texture = new RenderTexture(1280, 800, 24);
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
            File.WriteAllBytes("Temp/NetTestPreview.png", image.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
