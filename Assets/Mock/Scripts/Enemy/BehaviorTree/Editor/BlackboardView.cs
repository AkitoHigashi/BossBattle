using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTree.EditorTools
{
    /// <summary>
    ///  BlackBoard の中身を一覧表示するパネル
    ///  BlackBoard は BtAgent.Start で作られるので、値が出るのはPlay中だけ。Play外では項目と型だけ並べる
    ///  項目はリフレクションで集めるので、BlackBoard にプロパティを足せば自動で表示される
    /// </summary>
    [UxmlElement]
    public partial class BlackboardView : VisualElement
    {
        public BlackboardView()
        {
            AddToClassList("bt-blackboard");

            _message = new Label();
            _message.AddToClassList("bt-message");
            Add(_message);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("bt-panel__content");
            Add(scroll);

            foreach (var property in GetDisplayProperties(typeof(BlackBoard)))
            {
                // AttackContext のような入れ子のクラスは、中のプロパティを折りたたみで並べる
                if (IsNestedContext(property.PropertyType))
                {
                    var foldout = new Foldout { text = property.Name, value = true };
                    foldout.AddToClassList("bt-blackboard__group");
                    scroll.Add(foldout);

                    foreach (var child in GetDisplayProperties(property.PropertyType))
                    {
                        foldout.Add(CreateRow(child, bb => child.GetValue(property.GetValue(bb))));
                    }
                }
                else
                {
                    scroll.Add(CreateRow(property, bb => property.GetValue(bb)));
                }
            }

            SetBlackBoard(null);
        }

        /// <summary>
        ///  表示する黒板を切り替える
        /// </summary>
        /// <param name="blackBoard">実行中エージェントの黒板。null なら値を消して案内文を出す</param>
        public void SetBlackBoard(BlackBoard blackBoard)
        {
            _blackBoard = blackBoard;
            _message.text = blackBoard != null
                ? string.Empty
                : EditorApplication.isPlaying
                    ? "実行中のエージェントが見つかりません。"
                    : "Play中に、実行中エージェントの値を表示します。";
            _message.style.display = blackBoard != null ? DisplayStyle.None : DisplayStyle.Flex;
            Refresh();
        }

        /// <summary>
        ///  表示中の黒板から値を読み直す
        /// </summary>
        public void Refresh()
        {
            foreach (var row in _rows)
            {
                if (_blackBoard == null)
                {
                    row.Current = null;
                    row.ValueLabel.text = "-";
                    continue;
                }

                try
                {
                    row.Current = row.Getter(_blackBoard);
                    row.ValueLabel.text = Format(row.Current);
                }
                catch (Exception)
                {
                    row.Current = null;
                    row.ValueLabel.text = "(取得できません)";// 入れ子の親が null の時など
                }
            }
        }

        private VisualElement CreateRow(PropertyInfo property, Func<BlackBoard, object> getter)
        {
            var element = new VisualElement();
            element.AddToClassList("bt-blackboard__row");

            var nameColumn = new VisualElement();
            nameColumn.AddToClassList("bt-blackboard__name-column");
            element.Add(nameColumn);

            var nameLabel = new Label(property.Name);
            nameLabel.AddToClassList("bt-blackboard__name");
            nameColumn.Add(nameLabel);

            var typeLabel = new Label(GetTypeName(property.PropertyType));
            typeLabel.AddToClassList("bt-blackboard__type");
            nameColumn.Add(typeLabel);

            var valueLabel = new Label("-");
            valueLabel.AddToClassList("bt-blackboard__value");
            element.Add(valueLabel);

            var row = new Row { Getter = getter, ValueLabel = valueLabel };
            _rows.Add(row);

            // オブジェクト参照の行はクリックでHierarchy / Project上の場所を示す
            element.RegisterCallback<ClickEvent>(_ =>
            {
                var target = row.Current as UnityEngine.Object;
                if (target != null)
                {
                    EditorGUIUtility.PingObject(target);
                }
            });

            return element;
        }

        private static IEnumerable<PropertyInfo> GetDisplayProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        }

        /// <summary>
        ///  中身を展開して見せるクラスか。Unityのオブジェクトや値を包むだけの型（ReactiveProperty等）は除く
        /// </summary>
        private static bool IsNestedContext(Type type)
        {
            return type.IsClass &&
                   type != typeof(string) &&
                   !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                   GetWrappedValueProperty(type) == null;
        }

        /// <summary>
        ///  ReactiveProperty のように値を包んでいる型なら、中身を取り出すプロパティを返す
        /// </summary>
        private static PropertyInfo GetWrappedValueProperty(Type type)
        {
            return type.GetProperty("CurrentValue", BindingFlags.Public | BindingFlags.Instance) ??
                   type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        }

        private static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return "None";
                case UnityEngine.Object unityObject:
                    return unityObject != null ? unityObject.name : "Missing";// 破棄済みは == null になる
                case float f:
                    return f.ToString("0.##");
                case Vector2 v2:
                    return v2.ToString("F2");
                case Vector3 v3:
                    return v3.ToString("F2");
            }

            var wrapped = GetWrappedValueProperty(value.GetType());
            if (wrapped != null && wrapped.GetIndexParameters().Length == 0)
            {
                return Format(wrapped.GetValue(value));
            }

            return value.ToString();
        }

        /// <summary>
        ///  ReactiveProperty`1[System.Single] のような型名を ReactiveProperty&lt;float&gt; の形にする
        /// </summary>
        private static string GetTypeName(Type type)
        {
            if (TypeAliases.TryGetValue(type, out var alias))
            {
                return alias;
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeName))}>";
        }

        /// <summary> 1項目分の表示と、値の読み出し方 </summary>
        private sealed class Row
        {
            public Func<BlackBoard, object> Getter;
            public Label ValueLabel;
            public object Current;
        }

        private static readonly Dictionary<Type, string> TypeAliases = new Dictionary<Type, string>
        {
            { typeof(float), "float" },
            { typeof(int), "int" },
            { typeof(bool), "bool" },
            { typeof(string), "string" },
        };

        private readonly Label _message;
        private readonly List<Row> _rows = new List<Row>();
        private BlackBoard _blackBoard;
    }
}
