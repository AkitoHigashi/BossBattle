using UnityEngine;

public class CompositNode : BtNode
{
    [SerializeField] private BtNode[] _childNodes;
    public override void Initialize(BlackBoard blackBoard)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnUpdate()
    {
        throw new System.NotImplementedException();
    }
}
