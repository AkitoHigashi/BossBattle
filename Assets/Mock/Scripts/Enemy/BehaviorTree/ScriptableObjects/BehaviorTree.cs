using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorTree", menuName = "Behavior Tree/Mock/BehaviorTree")]
public class BehaviorTree : ScriptableObject
{
    [SerializeField] private RootNode _rootNode;
    [NonSerialized] private BlackBoard _blackBoard;
    [NonSerialized] private List<BtNode> _clonedNodes = new List<BtNode>();

    // グラフエディタに並ぶ全Node。Rootから辿れない未接続のNodeも保持する
    [SerializeField, HideInInspector] private List<BtNode> _nodes = new List<BtNode>();

    /// <summary>
    /// Clone()で複製した実行用Node。エディタのデバッグ表示に使う
    /// </summary>
    public IReadOnlyList<BtNode> ClonedNodes => _clonedNodes;

    /// <summary>
    /// 実行用のツリーを複製する
    /// エージェントごとにNodeの状態を独立させるため、実行前に必ず通す
    /// </summary>
    /// <returns>複製したツリー</returns>
    public BehaviorTree Clone()
    {
        var clone = Instantiate(this);

        // Instantiate後にフィールド初期化子が走る保証に頼らず、明示的に作る
        clone._clonedNodes = new List<BtNode>();
        clone._rootNode = _rootNode != null ? (RootNode)_rootNode.Clone(clone._clonedNodes) : null;

        return clone;
    }

    /// <summary>
    /// 複製した全Nodeに黒板を配る
    /// </summary>
    /// <param name="blackBoard">配る黒板</param>
    public void Initialize(BlackBoard blackBoard)
    {
        _blackBoard = blackBoard;

        if (_clonedNodes.Count == 0)
        {
            Debug.LogError("Clone()を通していないツリーをInitializeしています。", this);
            return;
        }

        // Clone()が全Nodeを_clonedNodesに登録しているので、Node側の再帰は不要
        foreach (var node in _clonedNodes)
        {
            node.Initialize(blackBoard);
        }
    }

    public void Update()
    {
        if (_rootNode == null)
        {
            return;
        }

        _rootNode.Evaluate();

        // Runningだったのに今フレーム評価されなかったNodeは、親が途中で呼ぶのをやめたNode
        // 条件が外れた等で中断されたので、途中の状態を捨てさせる
        var frame = Time.frameCount;
        foreach (var node in _clonedNodes)
        {
            if (node != null && node.IsRunning && node.LastEvaluatedFrame != frame)
            {
                node.Abort();
            }
        }
    }

    /// <summary>
    /// 実行途中の全Nodeを中断する。エージェントを止める時に呼ぶ
    /// </summary>
    public void AbortAll()
    {
        foreach (var node in _clonedNodes)
        {
            if (node != null)
            {
                node.Abort();
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var node in _clonedNodes)
        {
            if (node != null)
            {
                Destroy(node);
            }
        }

        _clonedNodes.Clear();
    }
}
