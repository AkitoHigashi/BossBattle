using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetworkTest_Yajima.Editor
{
    public static class NetTestTransitionSetup
    {
        public const string IngamePath = "Assets/Mock/Scenes/Net/Yajima/IngameTest.unity";
        public const string LobbyPath = "Assets/Mock/Scenes/Net/Yajima/NetTest.unity";

        [MenuItem("Tools/Yajima/Set Up Ingame Scene Transition %#i")]
        public static void Configure()
        {
            const string prefabPath = "Assets/Mock/Prefabs/Net/Yajima/NetTestConnection.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var transition = root.GetComponent<RoomSceneTransition>() ?? root.AddComponent<RoomSceneTransition>();
                var fields = new SerializedObject(transition);
                fields.FindProperty("_scenePath").stringValue = IngamePath;
                fields.FindProperty("_connectionUI").objectReferenceValue = root.GetComponentInChildren<NetTestUI>(true).gameObject;
                fields.ApplyModifiedPropertiesWithoutUndo();
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    if (text.name == "Footer") text.text = "Owner starts when every member is ready. START ROOM opens IngameTest for room members.";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // IngameTestはNetTestのコピーなので、移動先の重複した接続UIだけ取り除きます。
            var scene = SceneManager.GetSceneByPath(IngamePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(IngamePath, OpenSceneMode.Additive);
            foreach (GameObject gameObject in scene.GetRootGameObjects())
                if (gameObject.name == "NetTestConnection" && gameObject.GetComponent<RoomClient>() != null)
                    Object.DestroyImmediate(gameObject);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);

            var builds = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { LobbyPath, IngamePath })
            {
                builds.RemoveAll(entry => entry.path == path);
                builds.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[NetTestTransition] Scene transition configured.");
        }
    }
}
