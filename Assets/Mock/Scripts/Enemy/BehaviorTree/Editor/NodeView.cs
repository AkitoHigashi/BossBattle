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
        /// <summary> 親から接続を受けるポート。Root は親を持たないので null </summary>
        public Port InputPort { get; private set; }

        /// <summary> 子へ接続するポート。Action は子を持たないので null </summary>
        public Port OutputPort { get; private set; }

        public NodeView(string title, Vector2 position, string typeClass)
            : base(UxmlPath)
        {
            this.title = title;
            SetPosition(new Rect(position, Vector2.zero));
            AddToClassList(typeClass);// タイトル色を USS のクラスで切り替える

            // Root はツリーの起点なので親を持たない
            if (typeClass != RootClass)
            {
                InputPort = CreatePort(Direction.Input, Port.Capacity.Single);
                inputContainer.Add(InputPort);
            }

            // Action は末端なので子を持たない
            if (typeClass != ActionClass)
            {
                // 子を複数持てるのは Composite のみ。Decorator と Root は1つだけ
                var capacity = typeClass == CompositeClass ? Port.Capacity.Multi : Port.Capacity.Single;
                OutputPort = CreatePort(Direction.Output, capacity);
                outputContainer.Add(OutputPort);
            }
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
        private const string RootClass = "root";
        private const string ActionClass = "action";
        private const string CompositeClass = "composite";
    }
}
