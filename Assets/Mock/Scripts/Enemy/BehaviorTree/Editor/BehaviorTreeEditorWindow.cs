using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTree.EditorTools
{
    /// <summary>
    ///  BehaviorTree アセットをグラフで編集するウィンドウ
    ///  Play中は BtAgent が実行しているツリーの状態をグラフ上に表示する
    ///  左側に BlackBoard、右側に選択したNodeの詳細（シリアライズ変数の編集）を表示する
    /// </summary>
    public class BehaviorTreeEditorWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/Mock/UIBuilder/BehaviorTreeEditor.uxml";

        [SerializeField] private BehaviorTree _tree;// ドメインリロード後も開いていたツリーを保つ

        private BehaviorTreeGraph _graph;
        private BlackboardView _blackboardView;
        private NodeInspectorView _inspectorView;
        private ObjectField _treeField;
        private Label _agentLabel;
        private VisualElement _warningArea;
        private ToolbarButton _autoLayoutButton;
        private ToolbarButton _importButton;
        private BtAgent _debugAgent;
        private double _nextAgentSearchTime;
        private double _nextBlackboardRefreshTime;

        [MenuItem("Window/Behavior Tree")]
        public static BehaviorTreeEditorWindow Open() => GetWindow<BehaviorTreeEditorWindow>("BehaviorTree");

        /// <summary>
        ///  Projectウィンドウで BehaviorTree アセットをダブルクリックした時に開く
        /// </summary>
        [OnOpenAsset]
        public static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not BehaviorTree tree)
            {
                return false;
            }

            Open().SetTree(tree);
            return true;
        }

        public void CreateGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"UXMLが見つかりません: {UxmlPath}");
                return;
            }

            uxml.CloneTree(rootVisualElement);
            _graph = rootVisualElement.Q<BehaviorTreeGraph>();
            _blackboardView = rootVisualElement.Q<BlackboardView>();
            _inspectorView = rootVisualElement.Q<NodeInspectorView>();
            _graph.NodeSelectionChanged += _inspectorView.ShowNode;
            _graph.Populated += OnGraphPopulated;
            rootVisualElement.Insert(0, CreateToolbar());

            _warningArea = new VisualElement();
            _warningArea.AddToClassList("bt-warning-area");
            rootVisualElement.Insert(1, _warningArea);

            if (_tree != null)
            {
                SetTree(_tree);
            }
            else
            {
                OnSelectionChange();
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            BehaviorTreeAssetUtility.SaveTree(_tree);// このツリーと、使っている別ファイルのNodeアセットだけ保存する
        }

        /// <summary>
        ///  BehaviorTree アセット、または BtAgent を持つ GameObject を選択したらそのツリーを開く
        /// </summary>
        private void OnSelectionChange()
        {
            if (Selection.activeObject is BehaviorTree tree)
            {
                if (tree != _tree) SetTree(tree);
            }
            else if (Selection.activeGameObject != null &&
                     Selection.activeGameObject.TryGetComponent<BtAgent>(out var agent) &&
                     agent.SourceTree != null)
            {
                if (agent.SourceTree != _tree) SetTree(agent.SourceTree);
                _debugAgent = agent;
            }
        }

        private void Update()
        {
            if (_graph == null || !EditorApplication.isPlaying)
            {
                return;
            }

            var agent = FindDebugAgent();
            _graph.UpdateRuntimeStatus(agent != null ? agent.RuntimeTree : null);
            _agentLabel.text = agent != null ? $"実行中: {agent.name}" : "実行中のエージェントなし";

            // 黒板の値は毎フレーム読むほど細かく見る必要がないので間隔を空ける
            if (EditorApplication.timeSinceStartup >= _nextBlackboardRefreshTime)
            {
                _nextBlackboardRefreshTime = EditorApplication.timeSinceStartup + BlackboardRefreshInterval;
                _blackboardView.SetBlackBoard(agent != null ? agent.BlackBoard : null);
            }
        }

        private void SetTree(BehaviorTree tree)
        {
            // Play中の複製ツリーはアセットではないので編集対象にしない
            if (tree != null && !EditorUtility.IsPersistent(tree))
            {
                return;
            }

            if (_tree != tree)
            {
                _debugAgent = null;
            }

            _tree = tree;
            if (_graph == null)
            {
                return;// CreateGUI前。CreateGUIで改めて読み込む
            }

            _treeField.SetValueWithoutNotify(tree);
            _inspectorView.ShowNode(null);
            _graph.PopulateView(tree);
            _graph.schedule.Execute(() => _graph.FrameAll());// レイアウト確定後でないと全体が収まらない
        }

        /// <summary>
        ///  状態を表示するエージェントを決める
        ///  選択中のエージェントを優先し、無ければ同じツリーを使っているエージェントを探す
        /// </summary>
        private BtAgent FindDebugAgent()
        {
            if (_debugAgent != null && _debugAgent.SourceTree == _tree)
            {
                return _debugAgent;
            }

            // 毎フレーム全検索しないように間隔を空ける
            if (EditorApplication.timeSinceStartup < _nextAgentSearchTime)
            {
                return null;
            }

            _nextAgentSearchTime = EditorApplication.timeSinceStartup + AgentSearchInterval;
            foreach (var agent in FindObjectsByType<BtAgent>(FindObjectsInactive.Exclude))
            {
                if (agent.SourceTree == _tree)
                {
                    _debugAgent = agent;
                    return agent;
                }
            }

            return null;
        }

        private Toolbar CreateToolbar()
        {
            var toolbar = new Toolbar();

            _treeField = new ObjectField
            {
                objectType = typeof(BehaviorTree),
                allowSceneObjects = false,
                style = { width = 240 },
            };
            _treeField.RegisterValueChangedCallback(evt => SetTree(evt.newValue as BehaviorTree));
            toolbar.Add(_treeField);

            toolbar.Add(new ToolbarButton(Save) { text = "保存" });
            _autoLayoutButton = new ToolbarButton(AutoLayout) { text = "自動整列" };
            toolbar.Add(_autoLayoutButton);
            _importButton = new ToolbarButton(ImportExternalNodes)
            {
                text = "外部ノードを取り込む",
                tooltip = "別ファイルのノードアセットを複製して、このツリー専用のサブアセットにします。元のファイルは残ります。",
            };
            toolbar.Add(_importButton);
            toolbar.Add(new ToolbarButton(() => _graph?.FrameAll()) { text = "全体表示" });

            toolbar.Add(new ToolbarSpacer { flex = true });
            _agentLabel = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, marginRight = 6 } };
            toolbar.Add(_agentLabel);

            return toolbar;
        }

        private void Save()
        {
            BehaviorTreeAssetUtility.SaveTree(_tree);// このツリーと、使っている別ファイルのNodeアセットだけ保存する
        }

        private void AutoLayout()
        {
            if (_tree == null || _graph.IsReadOnly)
            {
                return;
            }

            BehaviorTreeAssetUtility.AutoLayout(_tree);
            _graph.PopulateView(_tree);
        }

        private void ImportExternalNodes()
        {
            if (_tree == null || _graph.IsReadOnly)
            {
                return;
            }

            var count = BehaviorTreeAssetUtility.GetNodes(_tree).Count(n => AssetDatabase.IsMainAsset(n));
            if (count == 0)
            {
                EditorUtility.DisplayDialog("外部ノードを取り込む", "このツリーは別ファイルのノードを使っていません。", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("外部ノードを取り込む",
                    $"別ファイルのノード {count} 個を複製して、「{_tree.name}」のサブアセットにします。\n" +
                    "元のファイルは残るので、他のツリーには影響しません。",
                    "取り込む", "キャンセル"))
            {
                return;
            }

            BehaviorTreeAssetUtility.ImportExternalNodes(_tree);
            _graph.PopulateView(_tree);
        }

        /// <summary>
        ///  グラフを作り直したら、警告と編集できるかどうかの表示を合わせる
        /// </summary>
        private void OnGraphPopulated()
        {
            _warningArea.Clear();
            foreach (var warning in _graph.Warnings)
            {
                _warningArea.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
            }

            _inspectorView.SetReadOnly(_graph.IsReadOnly);
            _autoLayoutButton?.SetEnabled(_tree != null && !_graph.IsReadOnly);
            _importButton?.SetEnabled(_tree != null && !_graph.IsReadOnly);
        }

        private void OnUndoRedo()
        {
            if (_tree != null && _graph != null)
            {
                _graph.PopulateView(_tree);
            }

            // Nodeの作成を取り消した時など、表示中のNodeが破棄されていたら表示を消す
            if (_inspectorView != null && _inspectorView.Node == null)
            {
                _inspectorView.ShowNode(null);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _debugAgent = null;
                _graph?.UpdateRuntimeStatus(null);
                if (_agentLabel != null) _agentLabel.text = string.Empty;
            }

            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                _blackboardView?.SetBlackBoard(null);// 案内文をPlay中 / Play外の内容に切り替える
                _inspectorView?.Rebuild();
            }
        }

        private const double AgentSearchInterval = 0.5;
        private const double BlackboardRefreshInterval = 0.1;
    }
}
