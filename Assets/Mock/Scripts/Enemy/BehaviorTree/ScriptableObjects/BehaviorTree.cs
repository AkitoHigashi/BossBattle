using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorTree", menuName = "Behavior Tree/Mock/BehaviorTree")]
public class BehaviorTree : ScriptableObject
{
    [SerializeField] private RootNode _rootNode;
    [SerializeField] private BlackBoard _blackBoard;
    [SerializeField] private List<BtNode> _nodes;


    public void Update()
    {
        if (_rootNode != null)
        {
            throw new System.NotImplementedException("RootNodeが実装されていません。");
        }

        _rootNode.Evaluate();
    }
}
