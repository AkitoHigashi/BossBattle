using System.Collections.Generic;
using UnityEngine;

public abstract class BtNode : ScriptableObject
{
    /// <summary>
    /// 現在のNodeの状態
    /// </summary>
    public NodeStatus Status { get; private set; } = NodeStatus.Running;

    /// <summary>
    /// Nodeを評価し、結果を記録して親へ返す
    /// </summary>
    /// <returns>評価結果</returns>
    public NodeStatus Evaluate()
    {
        Status = OnUpdate();

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
    protected BlackBoard _blackBoard;
}
