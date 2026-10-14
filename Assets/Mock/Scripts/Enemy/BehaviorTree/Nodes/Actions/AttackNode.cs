using UnityEngine;
[CreateAssetMenu(fileName = "AttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackNode")]
public abstract class AttackNode : ActionNode
{
    [SerializeField] private int _attackMultiplier = 10;
    protected int Damage => _attackMultiplier;
}
