using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviorTree.Mock
{
    public class BehaviorTreeEditorWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/Mock/UIBuilder/BehaviorTreeEditor.uxml";

        [MenuItem("Window/Behavior Tree")]
        public static void Open() => GetWindow<BehaviorTreeEditorWindow>("BehaviorTree");

        public void CreateGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"UXMLが見つかりません: {UxmlPath}");
                return;
            }

            uxml.CloneTree(rootVisualElement);
        }
    }
}
