using UnityEngine;
[System.Serializable, CreateAssetMenu(fileName = "RootNode", menuName = "Behavior Tree/Mock/Nodes/RootNode")]
public class RootNode : BtNode
{
    [SerializeField]private BtNode _childNode;

    protected override void OnUpdate()
    {
        _childNode.Evaluate();
    }
}
