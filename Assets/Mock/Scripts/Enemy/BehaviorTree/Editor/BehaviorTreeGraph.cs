using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace BTree.EditorTools
{
    /// <summary>
    ///  ウィンドウのグラフビューを表すクラス
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
        ///  コンテキストメニューの構築を行う
        ///  右クリックで表示されるメニューの内容を定義する
        /// </summary>
        /// <param name="evt">コンテキストメニューのイベント</param>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var pos = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Add Action", _ => AddElement(new NodeView("Action", pos, "action")));
            evt.menu.AppendAction("Add Composite", _ => AddElement(new NodeView("Composite", pos, "composite")));
            evt.menu.AppendAction("Add Decorator", _ => AddElement(new NodeView("Decorator", pos, "decorator")));
        }

        /// <summary>
        ///  ポートをドラッグして接続する際に、互換性のあるポートを取得する
        /// </summary>
        /// <param name="startPort"></param>
        /// <param name="nodeAdapter"></param>
        /// <returns></returns>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.Where(p => p.direction != startPort.direction &&
                                    p.node != startPort.node).ToList();
        }
    }
}
