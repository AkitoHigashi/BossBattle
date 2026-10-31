using UnityEngine;

/// <summary>
/// アビリティを実行するキャラクター固有の依存先をひとまとめにする。
/// </summary>
public sealed class SkillContext : System.IDisposable
{
    public CharacterMotor CharacterMotor { get; private set; }
    public Animator Animator { get; private set; }
    public AttackAnimationEvents AnimationEvents { get; private set; }
    // 溜めゲージの表示用データ。UI がなくても溜め処理が動くよう、常に生成しておく。
    public ChargeGauge ChargeGauge { get; } = new ChargeGauge();
    // 飛び道具の使い回し用。キャラクターと寿命を揃えるため、ここで持つ。
    public ProjectilePool ProjectilePool { get; } = new ProjectilePool();

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
        AnimationEvents?.CancelAttack();
        // 通常は ChargeAction の finally で隠れるが、即時中断の経路でもゲージが残らないようにする。
        ChargeGauge.Hide();

        if (CharacterMotor == null)
            return;

        CharacterMotor.ClearOverrideVelocity();
        CharacterMotor.ClearOverrideFacingDirection();
    }

    /// <summary>
    /// キャラクターの破棄時に、このコンテキストが持つリソース（飛び道具のプール）を片付ける。
    /// </summary>
    public void Dispose()
    {
        ProjectilePool.Dispose();
    }
}
