using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace BTree.EditorTools
{
    /// <summary>
    ///  グラフで選択したNodeの詳細を表示し、シリアライズ変数を編集するパネル
    ///  PropertyField を SerializedObject にバインドするので、Undo や [Tooltip] / [Min] などもそのまま効く
    /// </summary>
    [UxmlElement]
    public partial class NodeInspectorView : VisualElement
    {
        /// <summary> 表示中のNode。何も表示していなければ null </summary>
        public BtNode Node { get; private set; }

        public NodeInspectorView()
        {
            AddToClassList("bt-inspector");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("bt-panel__content");
            Add(_scroll);
            Rebuild();
        }

        /// <summary>
        ///  表示するNodeを切り替える。同じNodeなら作り直さない
        /// </summary>
        /// <param name="node">表示するNode。null なら案内文を出す</param>
        public void ShowNode(BtNode node)
        {
            if (node == Node && node != null)
            {
                return;
            }

            Node = node;
            Rebuild();
        }

        /// <summary>
        ///  編集できない状態にする。スクリプトが見つからないNodeを含むツリーでは、書き込むと参照が消えるため
        /// </summary>
        public void SetReadOnly(bool readOnly)
        {
            if (_readOnly == readOnly)
            {
                return;
            }

            _readOnly = readOnly;
            Rebuild();
        }

        /// <summary>
        ///  今のNodeで表示を作り直す。Playモードの切り替え時など、案内文を更新したい時に呼ぶ
        /// </summary>
        public void Rebuild()
        {
            _scroll.Clear();

            if (Node == null)
            {
                Node = null;// 破棄済みのNodeを持ち続けないようにする
                _scroll.Add(CreateMessage("ノードを選択すると、ここに詳細が表示されます。"));
                return;
            }

            _scroll.Add(CreateHeader(Node));

            if (EditorApplication.isPlaying)
            {
                _scroll.Add(new HelpBox(
                    "Play中の変更はアセットに保存されます。実行中のエージェントは開始時に複製したツリーを使うので、反映されるのは次の再生からです。",
                    HelpBoxMessageType.Info));
            }

            if (_readOnly)
            {
                _scroll.Add(new HelpBox("読み取り専用で開いているため編集できません。", HelpBoxMessageType.Warning));
            }
            else if (!BehaviorTreeAssetUtility.IsSubAsset(Node))
            {
                _scroll.Add(new HelpBox(
                    "別ファイルのノードアセットです。他のツリーでも使っている場合、変更はそちらにも反映されます。" +
                    "ツールバーの「外部ノードを取り込む」で、このツリー専用にできます。",
                    HelpBoxMessageType.Info));
            }

            var fields = new VisualElement();
            fields.AddToClassList("bt-inspector__fields");
            fields.SetEnabled(!_readOnly);
            _scroll.Add(fields);

            var serializedObject = new SerializedObject(Node);
            var property = serializedObject.GetIterator();
            var hasField = false;

            if (property.NextVisible(true))
            {
                do
                {
                    // スクリプト参照はヘッダーに出し、子の参照はグラフで繋ぐので除く
                    if (property.name == ScriptProperty || BehaviorTreeAssetUtility.IsChildProperty(property.name))
                    {
                        continue;
                    }

                    fields.Add(new PropertyField(property.Copy()));
                    hasField = true;
                }
                while (property.NextVisible(false));
            }

            if (!hasField)
            {
                fields.Add(CreateMessage("編集できるパラメータはありません。"));
            }

            fields.Bind(serializedObject);
        }

        /// <summary>
        ///  Nodeの種類・型名と、スクリプトを開くボタンを並べる
        /// </summary>
        private static VisualElement CreateHeader(BtNode node)
        {
            var header = new VisualElement();
            header.AddToClassList("bt-inspector__header");

            var kind = BehaviorTreeAssetUtility.GetKind(node.GetType());
            var kindLabel = new Label(kind.ToString());
            kindLabel.AddToClassList("bt-inspector__kind");
            kindLabel.AddToClassList(kind.ToString().ToLowerInvariant());// ノードと同じ色を付ける
            header.Add(kindLabel);

            var titleLabel = new Label(node.GetType().Name);
            titleLabel.AddToClassList("bt-inspector__title");
            header.Add(titleLabel);

            var buttons = new VisualElement();
            buttons.AddToClassList("bt-inspector__buttons");
            header.Add(buttons);

            var script = MonoScript.FromScriptableObject(node);
            if (script != null)
            {
                buttons.Add(new Button(() => AssetDatabase.OpenAsset(script)) { text = "スクリプトを開く" });
            }

            // 手作業で作った別ファイルのNodeアセットは、他のツリーと共有されている可能性がある
            if (!BehaviorTreeAssetUtility.IsSubAsset(node))
            {
                buttons.Add(new Button(() => EditorGUIUtility.PingObject(node)) { text = "アセットを表示" });

                var pathLabel = new Label(AssetDatabase.GetAssetPath(node));
                pathLabel.AddToClassList("bt-message");
                header.Add(pathLabel);
            }

            return header;
        }

        private static Label CreateMessage(string text)
        {
            var label = new Label(text);
            label.AddToClassList("bt-message");
            return label;
        }

        private const string ScriptProperty = "m_Script";
        private readonly ScrollView _scroll;
        private bool _readOnly;
    }
}
