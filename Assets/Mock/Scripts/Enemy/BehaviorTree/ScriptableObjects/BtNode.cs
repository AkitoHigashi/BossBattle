using System.Collections.Generic;
using UnityEngine;

public abstract class BtNode : ScriptableObject
{
    /// <summary>
    /// 現在のNodeの状態
    /// </summary>
    public NodeStatus Status { get; private set; } = NodeStatus.Running;

    /// <summary>
    /// 最後に評価されたフレーム。エディタのデバッグ表示で「今動いているか」の判定に使う
    /// </summary>
    public int LastEvaluatedFrame { get; private set; } = -1;

    /// <summary>
    /// グラフエディタ上でNodeを識別するID。Instantiateで複製しても同じ値になる
    /// </summary>
    public string Guid => _guid;

    /// <summary>
    /// 前回の評価でRunningを返し、まだ処理の途中かどうか
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Nodeを評価し、結果を記録して親へ返す
    /// </summary>
    /// <returns>評価結果</returns>
    public NodeStatus Evaluate()
    {
        LastEvaluatedFrame = Time.frameCount;
        Status = OnUpdate();
        IsRunning = Status == NodeStatus.Running;

        if (Status == NodeStatus.Success)
        {
            OnSuccess();
        }
        else if (Status == NodeStatus.Failure)
        {
            OnFailure();
        }

        return Status;
    }

    /// <summary>
    /// 処理の途中で親から呼ばれなくなったNodeを中断する
    /// 途中の状態を捨てて、次に評価された時は最初からやり直させる
    /// </summary>
    public void Abort()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        OnAbort();
    }

    public void Initialize(BlackBoard blackBoard)
    {
        _blackBoard = blackBoard;
    }

    /// <summary>
    /// 自分と子孫を複製する
    /// 子を持つNodeは子も複製し、戻り値を自分の子フィールドに差し込む
    /// </summary>
    /// <param name="clones">複製したNodeの登録先。破棄とInitializeに使う</param>
    /// <returns>複製したNode</returns>
    public virtual BtNode Clone(List<BtNode> clones)
    {
        var clone = Instantiate(this);
        clones.Add(clone);
        return clone;
    }

    protected abstract NodeStatus OnUpdate();
    protected virtual void OnSuccess() { }
    protected virtual void OnFailure() { }

    /// <summary>
    /// 中断された時に呼ばれる。途中の状態のリセットや、動かしていたものの停止を書く
    /// </summary>
    protected virtual void OnAbort() { }
    protected BlackBoard _blackBoard;

    // グラフエディタが書き込む。ランタイムの評価には使わない
    [SerializeField, HideInInspector] private string _guid;
    [SerializeField, HideInInspector] private Vector2 _graphPosition;
}
