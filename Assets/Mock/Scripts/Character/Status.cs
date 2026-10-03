
public class Status 
{
    public Status(int attackPower, int defensePower , int moveSpeed)
    {
        AttackPower = attackPower;
        DefensePower = defensePower;
        MoveSpeed = moveSpeed;
    }
   public int AttackPower { get; private set; }
   public int DefensePower { get; private set; }  
   public int MoveSpeed { get; private set; }
}
