
using System.Collections.Generic;

public class PlayerAttackSystem //: IAttackSystem
{
    public void Attack(AttackData attackData, IReadOnlyList<HitInfo> hitInfos)
    {
        foreach (var hitInfo in hitInfos)
        {
            var target = hitInfo.Target;
            
            foreach (var effect in attackData.HitEffects)
            {
                effect.Apply(attackData, hitInfo);
            }
        }
    }


    
}
