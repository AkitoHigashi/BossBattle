using UnityEngine;

/// <summary>
/// アビリティを実行するキャラクター固有の依存先をひとまとめにする。
/// </summary>
public sealed class SkillContext
{
    public CharacterMotor CharacterMotor { get; private set; }
    public Animator Animator { get; private set; }
    public AttackAnimationEvents AnimationEvents { get; private set; }
    
    public SkillContext(CharacterMotor motor, Animator animator, AttackAnimationEvents animationEvents)
    {
        CharacterMotor = motor ?? throw new System.ArgumentNullException(nameof(motor));
        Animator = animator ?? throw new System.ArgumentNullException(nameof(animator));
        AnimationEvents = animationEvents ?? throw new System.ArgumentNullException(nameof(animationEvents));
    }

    /// <summary>
    /// アビリティの実行が終了、中断された際に
    /// キャラクターを元の状態に戻すための処理を行う。
    /// </summary>
    public void AbortActiveAction()
    {
        if (AnimationEvents != null) AnimationEvents.CancelAttack();
        if (CharacterMotor != null) CharacterMotor.ClearOverrideVelocity();
    }
}
