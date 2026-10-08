using UnityEngine;
[CreateAssetMenu(fileName = "AttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackNode")]
public class AttackNode : ActionNode
{
    protected override NodeStatus OnUpdate()
    {
        Debug.Log("AttackNode: Attacking the target!");
        return NodeStatus.Success;
    }
}
