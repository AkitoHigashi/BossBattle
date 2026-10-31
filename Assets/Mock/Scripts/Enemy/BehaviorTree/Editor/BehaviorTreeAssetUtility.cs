using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BTree.EditorTools
{
    /// <summary> Nodeが子を何個持てるかで分けた種類 </summary>
    public enum NodeKind
    {
        Root,
        Composite,
        Decorator,
        Condition,
        Action,
    }

    /// <summary>
    ///  BehaviorTreeアセットとNodeアセットの読み書きをまとめたクラス
    ///  ランタイム側のフィールドは非公開なので SerializedObject 経由で触る（Undoにも乗る）
    /// </summary>
    public static class BehaviorTreeAssetUtility
    {
        public static NodeKind GetKind(Type type)
        {
            if (typeof(RootNode).IsAssignableFrom(type)) return NodeKind.Root;
            if (typeof(CompositeNode).IsAssignableFrom(type)) return NodeKind.Composite;
            if (typeof(DecoratorNode).IsAssignableFrom(type)) return NodeKind.Decorator;
            if (typeof(ConditionNode).IsAssignableFrom(type)) return NodeKind.Condition;
            return NodeKind.Action;
        }

        /// <summary>
        ///  グラフから作成できるNodeの型一覧。Rootはツリーに1つだけなので除く
        /// </summary>
        public static IEnumerable<Type> GetCreatableNodeTypes()
        {
            return TypeCache.GetTypesDerivedFrom<BtNode>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t != typeof(RootNode))
                .OrderBy(t => t.Name);
        }

        /// <summary>
        ///  子の参照を持つプロパティか。親子の接続はグラフで行うので、詳細パネルには出さない
        /// </summary>
        public static bool IsChildProperty(string propertyName)
        {
            return propertyName == ChildProperty || propertyName == ChildrenProperty;
        }

        public static bool IsSubAsset(BtNode node)
        {
            return !AssetDatabase.IsMainAsset(node);
        }

        public static RootNode GetRoot(BehaviorTree tree)
        {
            return new SerializedObject(tree).FindProperty(RootProperty).objectReferenceValue as RootNode;
        }

        public static List<BtNode> GetNodes(BehaviorTree tree)
        {
            var prop = new SerializedObject(tree).FindProperty(NodesProperty);
            var nodes = new List<BtNode>();

            for (var i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue is BtNode node)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        public static List<BtNode> GetChildren(BtNode node)
        {
            var children = new List<BtNode>();
            var so = new SerializedObject(node);

            switch (GetKind(node.GetType()))
            {
                case NodeKind.Composite:
                    var array = so.FindProperty(ChildrenProperty);
                    for (var i = 0; i < array.arraySize; i++)
                    {
                        if (array.GetArrayElementAtIndex(i).objectReferenceValue is BtNode child)
                        {
                            children.Add(child);
                        }
                    }
                    break;

                case NodeKind.Root:
                case NodeKind.Decorator:
                case NodeKind.Condition:
                    if (so.FindProperty(ChildProperty).objectReferenceValue is BtNode single)
                    {
                        children.Add(single);
                    }
                    break;
            }

            return children;
        }

        /// <summary>
        ///  子の参照を書き換える。Compositeは渡された順番がそのまま評価順になる
        /// </summary>
        public static void SetChildren(BtNode node, IReadOnlyList<BtNode> children)
        {
            var so = new SerializedObject(node);

            switch (GetKind(node.GetType()))
            {
                case NodeKind.Composite:
                    var array = so.FindProperty(ChildrenProperty);
                    array.arraySize = children.Count;
                    for (var i = 0; i < children.Count; i++)
                    {
                        array.GetArrayElementAtIndex(i).objectReferenceValue = children[i];
                    }
                    break;

                case NodeKind.Root:
                case NodeKind.Decorator:
                case NodeKind.Condition:
                    so.FindProperty(ChildProperty).objectReferenceValue = children.Count > 0 ? children[0] : null;
                    break;

                default:
                    return;// Actionは子を持たない
            }

            so.ApplyModifiedProperties();// 値が変わった時だけUndoに記録される
        }

        public static Vector2 GetPosition(BtNode node)
        {
            return new SerializedObject(node).FindProperty(PositionProperty).vector2Value;
        }

        public static void SetPosition(BtNode node, Vector2 position)
        {
            var so = new SerializedObject(node);
            so.FindProperty(PositionProperty).vector2Value = position;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        ///  Nodeを作成してツリーのサブアセットとして追加する
        /// </summary>
        public static BtNode CreateNode(BehaviorTree tree, Type type, Vector2 position)
        {
            var node = (BtNode)ScriptableObject.CreateInstance(type);
            node.name = type.Name;
            node.hideFlags = HideFlags.HideInHierarchy;// Projectウィンドウにサブアセットを並べない

            var so = new SerializedObject(node);
            so.FindProperty(GuidProperty).stringValue = GUID.Generate().ToString();
            so.FindProperty(PositionProperty).vector2Value = position;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(node, tree);
            Undo.RegisterCreatedObjectUndo(node, "Create Behavior Tree Node");

            var treeSo = new SerializedObject(tree);
            var nodes = treeSo.FindProperty(NodesProperty);
            nodes.arraySize++;
            nodes.GetArrayElementAtIndex(nodes.arraySize - 1).objectReferenceValue = node;
            treeSo.ApplyModifiedProperties();

            return node;
        }

        /// <summary>
        ///  ツリーからNodeを外す。ツリーのサブアセットなら破棄し、別ファイルのNodeアセットは残す
        /// </summary>
        public static void DeleteNode(BehaviorTree tree, BtNode node)
        {
            var treeSo = new SerializedObject(tree);
            var nodes = treeSo.FindProperty(NodesProperty);

            for (var i = nodes.arraySize - 1; i >= 0; i--)
            {
                if (nodes.GetArrayElementAtIndex(i).objectReferenceValue == node)
                {
                    nodes.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    nodes.DeleteArrayElementAtIndex(i);
                }
            }

            treeSo.ApplyModifiedProperties();

            if (IsSubAssetOf(node, tree))
            {
                Undo.DestroyObjectImmediate(node);
            }
        }

        /// <summary>
        ///  グラフ表示に必要な状態を揃える
        ///  Rootが無ければ作成し、手作業で繋いだ既存ツリーのNodeも一覧に取り込む
        /// </summary>
        public static void EnsureSetup(BehaviorTree tree)
        {
            var treeSo = new SerializedObject(tree);
            var rootProp = treeSo.FindProperty(RootProperty);
            var changed = false;

            if (rootProp.objectReferenceValue == null)
            {
                var root = ScriptableObject.CreateInstance<RootNode>();
                root.name = nameof(RootNode);
                root.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(root, tree);
                rootProp.objectReferenceValue = root;
                changed = true;
            }

            // 一覧にある未接続Node + Rootから辿れる全Node
            var known = GetNodes(tree);
            var reachable = new List<BtNode>();
            CollectDescendants((BtNode)rootProp.objectReferenceValue, reachable, new HashSet<BtNode>());
            var all = known.Union(reachable).ToList();

            var nodesProp = treeSo.FindProperty(NodesProperty);
            if (!all.SequenceEqual(known) || nodesProp.arraySize != known.Count)
            {
                nodesProp.arraySize = all.Count;
                for (var i = 0; i < all.Count; i++)
                {
                    nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                }
                changed = true;
            }

            treeSo.ApplyModifiedPropertiesWithoutUndo();

            // IDが無い、または複製で重複したNodeにIDを振り直す
            var usedGuids = new HashSet<string>();
            foreach (var node in all)
            {
                var so = new SerializedObject(node);
                var guid = so.FindProperty(GuidProperty);
                if (string.IsNullOrEmpty(guid.stringValue) || !usedGuids.Add(guid.stringValue))
                {
                    guid.stringValue = GUID.Generate().ToString();
                    usedGuids.Add(guid.stringValue);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            // 位置情報を持たない既存ツリーは見やすく並べておく
            if (all.Count > 1 && all.All(n => GetPosition(n) == Vector2.zero))
            {
                AutoLayout(tree, false);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(tree);
                SaveTree(tree);
            }
        }

        /// <summary>
        ///  ツリーと、それが使う別ファイルのNodeアセットだけを保存する
        ///  AssetDatabase.SaveAssets() だとプロジェクト中の未保存アセットまで保存してしまう
        /// </summary>
        public static void SaveTree(BehaviorTree tree)
        {
            if (tree == null)
            {
                return;
            }

            AssetDatabase.SaveAssetIfDirty(tree);// サブアセットも同じファイルなので一緒に保存される

            foreach (var node in GetNodes(tree).Where(n => AssetDatabase.IsMainAsset(n)))
            {
                AssetDatabase.SaveAssetIfDirty(node);
            }
        }

        /// <summary>
        ///  スクリプトが見つからないオブジェクトを含むアセットのパスを返す
        ///  そうした参照は SerializedObject から null にしか見えず、1度でも Apply すると fileID: 0 に書き換わって消える。
        ///  なので1つでも見つかったら、そのツリーには一切書き込まない
        /// </summary>
        public static List<string> FindAssetsWithMissingScripts(BehaviorTree tree)
        {
            var treePath = AssetDatabase.GetAssetPath(tree);
            return AssetDatabase.GetDependencies(treePath, true)
                .Append(treePath)
                .Where(p => p.EndsWith(".asset"))
                .Distinct()
                .Where(p => AssetDatabase.LoadAllAssetsAtPath(p).Any(o => o == null))
                .ToList();
        }

        /// <summary>
        ///  エディタからは作れないが、手作業で繋いだアセットには起こりうる構造の問題を探す
        /// </summary>
        /// <returns>問題の説明文。無ければ空</returns>
        public static List<string> FindStructureProblems(BehaviorTree tree)
        {
            var problems = new List<string>();
            var nodes = CollectNodes(tree);

            // 1つのNodeを複数の親が参照している
            var parentCounts = new Dictionary<BtNode, int>();
            foreach (var node in nodes)
            {
                foreach (var child in GetChildren(node))
                {
                    parentCounts.TryGetValue(child, out var count);
                    parentCounts[child] = count + 1;
                }
            }

            foreach (var pair in parentCounts.Where(p => p.Value > 1))
            {
                problems.Add($"「{pair.Key.name}」が {pair.Value} 個の親から参照されています。実行時は親ごとに別々に複製されます。");
            }

            // 参照が循環している
            var done = new HashSet<BtNode>();
            foreach (var node in nodes)
            {
                FindCycles(node, new HashSet<BtNode>(), done, problems);
            }

            return problems;
        }

        /// <summary>
        ///  一覧にあるNodeと、Rootから辿れるNodeを全て集める。アセットには書き込まない
        /// </summary>
        public static List<BtNode> CollectNodes(BehaviorTree tree)
        {
            var reachable = new List<BtNode>();
            CollectDescendants(GetRoot(tree), reachable, new HashSet<BtNode>());
            return GetNodes(tree).Union(reachable).ToList();
        }

        /// <summary>
        ///  別ファイルのNodeアセットを複製してツリーのサブアセットにし、参照を差し替える
        ///  元のファイルはそのまま残すので、他のツリーで使っていても影響しない
        /// </summary>
        /// <returns>取り込んだNodeの数</returns>
        public static int ImportExternalNodes(BehaviorTree tree)
        {
            var nodes = GetNodes(tree);
            var externals = nodes.Where(n => AssetDatabase.IsMainAsset(n)).ToList();
            if (externals.Count == 0)
            {
                return 0;
            }

            var copies = new Dictionary<BtNode, BtNode>();
            foreach (var external in externals)
            {
                var copy = UnityEngine.Object.Instantiate(external);
                copy.name = external.name;// "(Clone)" を付けない
                copy.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(copy, tree);
                Undo.RegisterCreatedObjectUndo(copy, "Import External Behavior Tree Nodes");
                copies[external] = copy;
            }

            BtNode Replace(BtNode node) => node != null && copies.TryGetValue(node, out var copy) ? copy : node;

            // 複製した側も中身は元のNodeを指しているので、全員の子を差し替える
            var replaced = nodes.Select(Replace).ToList();
            foreach (var node in replaced)
            {
                SetChildren(node, GetChildren(node).Select(Replace).ToList());
            }

            var treeSo = new SerializedObject(tree);
            var rootProp = treeSo.FindProperty(RootProperty);
            rootProp.objectReferenceValue = Replace(rootProp.objectReferenceValue as BtNode);

            var nodesProp = treeSo.FindProperty(NodesProperty);
            nodesProp.arraySize = replaced.Count;
            for (var i = 0; i < replaced.Count; i++)
            {
                nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = replaced[i];
            }

            treeSo.ApplyModifiedProperties();
            SaveTree(tree);
            return externals.Count;
        }

        private static void FindCycles(BtNode node, HashSet<BtNode> path, HashSet<BtNode> done, List<string> problems)
        {
            if (node == null || done.Contains(node))
            {
                return;
            }

            path.Add(node);
            foreach (var child in GetChildren(node))
            {
                if (path.Contains(child))
                {
                    problems.Add($"「{node.name}」→「{child.name}」で参照が循環しています。実行すると Clone() が無限に再帰します。");
                    continue;
                }

                FindCycles(child, path, done, problems);
            }

            path.Remove(node);
            done.Add(node);
        }

        /// <summary>
        ///  Rootから木の形に並べ直す。未接続のNodeは右側にまとめる
        /// </summary>
        public static void AutoLayout(BehaviorTree tree, bool recordUndo = true)
        {
            var nodes = GetNodes(tree);
            if (recordUndo)
            {
                Undo.RecordObjects(nodes.Cast<UnityEngine.Object>().ToArray(), "Auto Layout Behavior Tree");
            }

            var placed = new HashSet<BtNode>();
            var nextX = 0f;
            var root = GetRoot(tree);
            if (root != null)
            {
                LayoutSubtree(root, 0, ref nextX, placed);
            }

            foreach (var node in nodes.Where(n => !placed.Contains(n)))
            {
                LayoutSubtree(node, 0, ref nextX, placed);
            }
        }

        private static float LayoutSubtree(BtNode node, int depth, ref float nextX, HashSet<BtNode> placed)
        {
            placed.Add(node);
            var children = GetChildren(node).Where(c => !placed.Contains(c)).ToList();
            float x;

            if (children.Count == 0)
            {
                x = nextX;
                nextX += LayoutSpacing.x;
            }
            else
            {
                // 親は子の中央に置く
                var xs = new List<float>();
                foreach (var child in children)
                {
                    xs.Add(LayoutSubtree(child, depth + 1, ref nextX, placed));
                }
                x = (xs[0] + xs[xs.Count - 1]) * 0.5f;
            }

            var so = new SerializedObject(node);
            so.FindProperty(PositionProperty).vector2Value = new Vector2(x, depth * LayoutSpacing.y);
            so.ApplyModifiedPropertiesWithoutUndo();
            return x;
        }

        private static void CollectDescendants(BtNode node, List<BtNode> result, HashSet<BtNode> visited)
        {
            if (node == null || !visited.Add(node))
            {
                return;
            }

            result.Add(node);
            foreach (var child in GetChildren(node))
            {
                CollectDescendants(child, result, visited);
            }
        }

        private static bool IsSubAssetOf(BtNode node, BehaviorTree tree)
        {
            return AssetDatabase.GetAssetPath(node) == AssetDatabase.GetAssetPath(tree) &&
                   !AssetDatabase.IsMainAsset(node);
        }

        private const string RootProperty = "_rootNode";
        private const string NodesProperty = "_nodes";
        private const string ChildProperty = "_childNode";
        private const string ChildrenProperty = "_childNodes";
        private const string GuidProperty = "_guid";
        private const string PositionProperty = "_graphPosition";
        private static readonly Vector2 LayoutSpacing = new Vector2(200f, 240f);
    }
}
