using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTree.EditorTools
{
    /// <summary>
    ///  ウィンドウのグラフビューを表すクラス
    ///  グラフ上の操作をそのまま BehaviorTree アセットへ書き込む
    /// </summary>
    [UxmlElement]
    public partial class BehaviorTreeGraph : GraphView
    {
        public BehaviorTreeGraph()
        {
            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
        }

        /// <summary>
        ///  ツリーの内容でグラフを作り直す
        /// </summary>
        /// <param name="tree">表示するツリー。null ならグラフを空にする</param>
        public void PopulateView(BehaviorTree tree)
        {
            // Undoなどで作り直しても、選んでいたNodeが残っていれば選択し直す
            var selectedNode = _tree == tree ? selection.OfType<NodeView>().LastOrDefault()?.NodeAsset : null;
            _tree = tree;

            ClearSelection();
            graphViewChanged -= OnGraphViewChanged;// 作り直しでアセットを書き換えないように外す
            DeleteElements(graphElements.ToList());
            _views.Clear();
            _warnings.Clear();
            IsReadOnly = false;

            if (tree == null)
            {
                Populated?.Invoke();
                return;
            }

            // スクリプトが見つからないNodeへの参照は、書き込んだ瞬間に消えてしまうので読み取り専用で開く
            var missing = BehaviorTreeAssetUtility.FindAssetsWithMissingScripts(tree);
            if (missing.Count > 0)
            {
                IsReadOnly = true;
                _warnings.Add("スクリプトが見つからないノードがあるため、読み取り専用で開いています。" +
                              "クラス名を変えた・消した場合は元に戻してください。編集すると、そのノードへの参照が消えます。\n" +
                              string.Join("\n", missing.Select(p => $"・{p}")));
            }
            else
            {
                BehaviorTreeAssetUtility.EnsureSetup(tree);
            }

            _warnings.AddRange(BehaviorTreeAssetUtility.FindStructureProblems(tree));

            var nodes = BehaviorTreeAssetUtility.CollectNodes(tree);
            foreach (var node in nodes)
            {
                var view = new NodeView(node);
                _views[node] = view;
                AddElement(view);
            }

            foreach (var node in nodes)
            {
                var parentView = _views[node];
                foreach (var child in BehaviorTreeAssetUtility.GetChildren(node))
                {
                    if (parentView.OutputPort != null && _views.TryGetValue(child, out var childView) && childView.InputPort != null)
                    {
                        AddElement(parentView.OutputPort.ConnectTo(childView.InputPort));
                    }
                }
            }

            graphViewChanged += OnGraphViewChanged;

            if (selectedNode != null && _views.TryGetValue(selectedNode, out var selectedView))
            {
                AddToSelection(selectedView);
            }

            Populated?.Invoke();
        }

        /// <summary> スクリプトが見つからないNodeがあり、アセットに書き込まない状態か </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary> 開いているツリーの警告。読み取り専用の理由や、構造の問題 </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary> PopulateView でグラフを作り直した </summary>
        public event Action Populated;

        /// <summary>
        ///  NodeViewの選択・選択解除で呼ばれる
        ///  複数の選択変更が1回の操作で続けて届くので、次のフレームにまとめて1回だけ通知する
        /// </summary>
        public void NotifySelectionChanged()
        {
            if (_selectionNotifyScheduled)
            {
                return;
            }

            _selectionNotifyScheduled = true;
            schedule.Execute(() =>
            {
                _selectionNotifyScheduled = false;
                NodeSelectionChanged?.Invoke(selection.OfType<NodeView>().LastOrDefault()?.NodeAsset);
            });
        }

        /// <summary> 選択中のNodeが変わった。何も選んでいなければ null </summary>
        public event Action<BtNode> NodeSelectionChanged;

        /// <summary>
        ///  実行中のツリーの状態を各Nodeに反映する。Clone後もGuidが同じなのでそれで対応を取る
        /// </summary>
        /// <param name="runtimeTree">BtAgentが実行中のツリー。null なら表示を消す</param>
        public void UpdateRuntimeStatus(BehaviorTree runtimeTree)
        {
            _runtimeNodes.Clear();
            if (runtimeTree != null)
            {
                foreach (var node in runtimeTree.ClonedNodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.Guid))
                    {
                        continue;
                    }

                    // 複数の親から参照されたNodeは同じGuidで複数複製されるので、直近に評価された方を見せる
                    if (!_runtimeNodes.TryGetValue(node.Guid, out var other) || node.LastEvaluatedFrame > other.LastEvaluatedFrame)
                    {
                        _runtimeNodes[node.Guid] = node;
                    }
                }
            }

            foreach (var view in _views.Values)
            {
                _runtimeNodes.TryGetValue(view.NodeAsset.Guid ?? string.Empty, out var runtimeNode);
                view.SetRuntimeStatus(runtimeNode);
            }
        }

        /// <summary>
        ///  コンテキストメニューの構築を行う
        ///  右クリックで表示されるメニューの内容を定義する
        /// </summary>
        /// <param name="evt">コンテキストメニューのイベント</param>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Node上の右クリックでは削除などの標準メニューだけ出す
            if (evt.target == this)
            {
                if (_tree == null)
                {
                    evt.menu.AppendAction("BehaviorTree を選択してください", null, DropdownMenuAction.Status.Disabled);
                    return;
                }

                if (IsReadOnly)
                {
                    evt.menu.AppendAction("読み取り専用のためノードを追加できません", null, DropdownMenuAction.Status.Disabled);
                    return;
                }

                var pos = contentViewContainer.WorldToLocal(evt.mousePosition);
                foreach (var type in BehaviorTreeAssetUtility.GetCreatableNodeTypes())
                {
                    var category = BehaviorTreeAssetUtility.GetKind(type);
                    evt.menu.AppendAction($"{category}/{type.Name}", _ => CreateNode(type, pos));
                }

                evt.menu.AppendSeparator();
            }

            base.BuildContextualMenu(evt);
        }

        /// <summary>
        ///  ポートをドラッグして接続する際に、互換性のあるポートを取得する
        ///  循環すると Clone() が無限に再帰するので、祖先へは繋げない
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.Where(p => p.direction != startPort.direction &&
                                    p.node != startPort.node &&
                                    !WouldCreateCycle(startPort, p)).ToList();
        }

        private void CreateNode(Type type, Vector2 position)
        {
            var node = BehaviorTreeAssetUtility.CreateNode(_tree, type, position);
            var view = new NodeView(node);
            _views[node] = view;
            AddElement(view);
        }

        /// <summary>
        ///  グラフの変更をアセットへ反映する
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_tree == null)
            {
                return change;
            }

            // 読み取り専用では削除と接続を取り消す。移動は見た目だけで保存しない
            if (IsReadOnly)
            {
                change.elementsToRemove?.Clear();
                change.edgesToCreate?.Clear();
                return change;
            }

            var undoGroup = Undo.GetCurrentGroup();
            var removedEdges = new HashSet<Edge>();
            var removedViews = new HashSet<NodeView>();
            var createdEdges = change.edgesToCreate ?? new List<Edge>();
            var dirtyParents = new HashSet<NodeView>();

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge)
                    {
                        removedEdges.Add(edge);
                        if (edge.output?.node is NodeView parent) dirtyParents.Add(parent);
                    }
                    else if (element is NodeView view)
                    {
                        removedViews.Add(view);
                    }
                }
            }

            foreach (var edge in createdEdges)
            {
                if (edge.output?.node is NodeView parent) dirtyParents.Add(parent);
            }

            if (change.movedElements != null)
            {
                foreach (var view in change.movedElements.OfType<NodeView>())
                {
                    BehaviorTreeAssetUtility.SetPosition(view.NodeAsset, view.GetPosition().position);

                    // 横位置が評価順なので、動かしたNodeの親は子を並べ直す
                    if (GetParent(view) is NodeView parent) dirtyParents.Add(parent);
                }
            }

            foreach (var parent in dirtyParents.Where(p => !removedViews.Contains(p)))
            {
                SyncChildren(parent, removedEdges, createdEdges);
            }

            foreach (var view in removedViews)
            {
                _views.Remove(view.NodeAsset);
                BehaviorTreeAssetUtility.DeleteNode(_tree, view.NodeAsset);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return change;
        }

        /// <summary>
        ///  出力ポートの接続から子の参照を作り直す。Compositeは左にあるNodeから順に評価される
        /// </summary>
        private void SyncChildren(NodeView parent, HashSet<Edge> removedEdges, List<Edge> createdEdges)
        {
            if (parent.OutputPort == null)
            {
                return;
            }

            var children = parent.OutputPort.connections
                .Where(e => !removedEdges.Contains(e))
                .Concat(createdEdges.Where(e => e.output == parent.OutputPort))
                .Select(e => e.input.node)
                .OfType<NodeView>()
                .Distinct()
                .OrderBy(v => v.GetPosition().x)
                .Select(v => v.NodeAsset)
                .ToList();

            BehaviorTreeAssetUtility.SetChildren(parent.NodeAsset, children);
        }

        private static NodeView GetParent(NodeView view)
        {
            return view.InputPort?.connections.FirstOrDefault()?.output.node as NodeView;
        }

        /// <summary>
        ///  接続先の子が、接続元の親の祖先（または同じNode）なら循環になる
        /// </summary>
        private static bool WouldCreateCycle(Port startPort, Port targetPort)
        {
            var parent = (startPort.direction == Direction.Output ? startPort.node : targetPort.node) as NodeView;
            var child = (startPort.direction == Direction.Output ? targetPort.node : startPort.node) as NodeView;

            // 手作業で循環させたアセットでも無限ループしないよう、辿ったNodeを覚えておく
            var visited = new HashSet<NodeView>();
            for (var current = parent; current != null && visited.Add(current); current = GetParent(current))
            {
                if (current == child)
                {
                    return true;
                }
            }

            return false;
        }

        // コピー・カット・貼り付け・複製は未対応。有効なままだとカットでNodeが消えるだけになる
        protected override bool canCopySelection => false;
        protected override bool canCutSelection => false;
        protected override bool canPaste => false;
        protected override bool canDuplicateSelection => false;

        private BehaviorTree _tree;
        private bool _selectionNotifyScheduled;
        private readonly Dictionary<BtNode, NodeView> _views = new Dictionary<BtNode, NodeView>();
        private readonly Dictionary<string, BtNode> _runtimeNodes = new Dictionary<string, BtNode>();
        private readonly List<string> _warnings = new List<string>();
    }
}
