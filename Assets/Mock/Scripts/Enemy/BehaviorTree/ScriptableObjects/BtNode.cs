using UnityEngine;

public abstract class BtNode : ScriptableObject
{
    [SerializeField] private BtNode _childNode;
    /// <summary>
    /// 現在のNodeの状態
    /// </summary>
    public NodeStatus Status { get; private set; } = NodeStatus.Running;
    /// <summary>
    /// Nodeの状態を見て処理を実行させる
    /// </summary>
    /// <returns></returns>
    public NodeStatus Evaluate()
    {
        OnUpdate();

        if(Status == NodeStatus.Success)
        {
            OnSuccess();
        }
        else if(Status == NodeStatus.Failure)
        {
            OnFailure();
        }

        return Status;
    }
    public abstract void Initialize(BlackBoard blackBoard);
    protected abstract void OnUpdate();
    protected virtual void OnSuccess() { }
    protected virtual void OnFailure() { }
    protected BlackBoard _blackBoard;
}
