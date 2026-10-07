using UnityEngine;
[System.Serializable, CreateAssetMenu(fileName = "RootNode", menuName = "Behavior Tree/Mock/Nodes/RootNode")]
public sealed class RootNode : BtNode
{
    [SerializeField]private BtNode _childNode;

    public override void Initialize(BlackBoard blackBoard)
    {
        _blackBoard = blackBoard;
        _childNode.Initialize(_blackBoard);
    }

    protected override void OnUpdate()
    {
        _childNode.Evaluate();
    }
}
