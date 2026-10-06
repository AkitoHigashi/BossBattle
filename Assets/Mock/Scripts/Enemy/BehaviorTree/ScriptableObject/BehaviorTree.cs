using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorTree", menuName = "Behavior Tree/Mock/BehaviorTree")]
public class BehaviorTree : ScriptableObject
{
    [SerializeField] private BtNode _rootNode;
    [SerializeField] private BlackBoard _blackBoard;

    [SerializeField] private List<BtNode> _nodes;
}
