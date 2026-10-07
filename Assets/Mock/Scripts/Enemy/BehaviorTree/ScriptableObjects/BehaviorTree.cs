using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorTree", menuName = "Behavior Tree/Mock/BehaviorTree")]
public class BehaviorTree : ScriptableObject
{
    [SerializeField] private RootNode _rootNode;
    private BlackBoard _blackBoard;
    public void Initialize(BlackBoard blackBoard)
    {
        _blackBoard = blackBoard;
        _rootNode.Initialize(_blackBoard);
    }

    public void Update()
    {
        if (_rootNode != null)
        {
            throw new System.NotImplementedException("RootNodeが実装されていません。");
        }

        _rootNode.Evaluate();
    }
}
