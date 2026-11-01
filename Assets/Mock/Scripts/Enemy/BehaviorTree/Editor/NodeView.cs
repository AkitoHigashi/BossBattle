using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BTree.EditorTools
{
    /// <summary>
    ///  グラフ上に表示する1ノードのビュー
    ///  見た目は NodeView.uxml / NodeView.uss で定義する
    /// </summary>
    public class NodeView : Node
    {
        /// <summary> このビューが表すNodeアセット </summary>
        public BtNode NodeAsset { get; }

        /// <summary> 親から接続を受けるポート。Root は親を持たないので null </summary>
        public Port InputPort { get; private set; }

        /// <summary> 子へ接続するポート。Action は子を持たないので null </summary>
        public Port OutputPort { get; private set; }

        public NodeView(BtNode nodeAsset)
            : base(UxmlPath)
        {
            NodeAsset = nodeAsset;
            viewDataKey = nodeAsset.Guid;
            title = GetDisplayName(nodeAsset);
            tooltip = nodeAsset.GetType().Name;
            SetPosition(new Rect(BehaviorTreeAssetUtility.GetPosition(nodeAsset), Vector2.zero));

            var kind = BehaviorTreeAssetUtility.GetKind(nodeAsset.GetType());
            AddToClassList(kind.ToString().ToLowerInvariant());// タイトル色を USS のクラスで切り替える

            // Root はツリーの起点なので親を持たず、消すこともできない
            if (kind == NodeKind.Root)
            {
                capabilities &= ~Capabilities.Deletable;
            }
            else
            {
                InputPort = CreatePort(Direction.Input, Port.Capacity.Single);
                inputContainer.Add(InputPort);
            }

            // Action は末端なので子を持たない
            if (kind != NodeKind.Action)
            {
                // 子を複数持てるのは Composite のみ。Decorator / Condition / Root は1つだけ
                var capacity = kind == NodeKind.Composite ? Port.Capacity.Multi : Port.Capacity.Single;
                OutputPort = CreatePort(Direction.Output, capacity);
                outputContainer.Add(OutputPort);
            }
        }

        /// <summary>
        ///  選択状態の変化をグラフに伝え、ウィンドウ左の詳細パネルを切り替えてもらう
        /// </summary>
        public override void OnSelected()
        {
            base.OnSelected();
            GetFirstAncestorOfType<BehaviorTreeGraph>()?.NotifySelectionChanged();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            GetFirstAncestorOfType<BehaviorTreeGraph>()?.NotifySelectionChanged();
        }

        /// <summary>
        ///  Play中の実行状態を枠の色に反映する。直近フレームで評価されていなければ表示しない
        /// </summary>
        /// <param name="runtimeNode">対応する実行中のNode。無ければ null</param>
        public void SetRuntimeStatus(BtNode runtimeNode)
        {
            string statusClass = null;
            if (runtimeNode != null && Time.frameCount - runtimeNode.LastEvaluatedFrame <= 1)
            {
                statusClass = runtimeNode.Status switch
                {
                    NodeStatus.Success => SuccessClass,
                    NodeStatus.Failure => FailureClass,
                    _ => RunningClass,
                };
            }

            // 毎フレーム呼ばれるので、変わった時だけクラスを付け替える（付け替えのたびにスタイルが再計算される）
            if (statusClass == _statusClass)
            {
                return;
            }

            if (_statusClass != null) RemoveFromClassList(_statusClass);
            if (statusClass != null) AddToClassList(statusClass);
            _statusClass = statusClass;
        }

        /// <summary>
        ///  型名の末尾の "Node" を省いた表示名。サブアセットでない既存アセットはアセット名も添える
        /// </summary>
        private static string GetDisplayName(BtNode nodeAsset)
        {
            var typeName = nodeAsset.GetType().Name;
            if (typeName.EndsWith("Node") && typeName.Length > 4)
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }

            return AssetDatabase.IsMainAsset(nodeAsset) && nodeAsset.name != nodeAsset.GetType().Name
                ? $"{typeName}\n({nodeAsset.name})"
                : typeName;
        }

        /// <summary>
        ///  上下方向に並ぶポートを生成する
        /// </summary>
        /// <param name="direction">ポートの向き</param>
        /// <param name="capacity">接続できるエッジの本数</param>
        /// <returns>生成したポート</returns>
        private Port CreatePort(Direction direction, Port.Capacity capacity)
        {
            var port = InstantiatePort(Orientation.Vertical, direction, capacity, typeof(bool));
            port.portName = string.Empty;// 縦向きポートではラベルを使わない
            return port;
        }

        private const string UxmlPath = "Assets/Mock/UIBuilder/NodeView.uxml";
        private const string RunningClass = "running";
        private const string SuccessClass = "success";
        private const string FailureClass = "failure";
        private string _statusClass;
    }
}
