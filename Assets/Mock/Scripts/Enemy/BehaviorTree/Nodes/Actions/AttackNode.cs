using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AttackNode : ActionNode
{
    protected override NodeStatus OnUpdate()
    {
        return NodeStatus.Success;
    }
}
