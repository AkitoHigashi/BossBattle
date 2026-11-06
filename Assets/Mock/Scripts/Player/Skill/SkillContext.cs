using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アビリティを実行するキャラクター固有の依存先をひとまとめにする。
/// </summary>
public sealed class SkillContext : IDisposable
{
    public CharacterMotor CharacterMotor { get; private set; }
    public Animator Animator { get; private set; }
    public AttackAnimationEvents AnimationEvents { get; private set; }
    // 溜めゲージの表示用データ。UI がなくても溜め処理が動くよう、常に生成しておく。
    public ChargeGauge ChargeGauge { get; } = new ChargeGauge();
    // 飛び道具の使い回し用。キャラクターと寿命を揃えるため、ここで持つ。
    public ProjectilePool ProjectilePool { get; } = new ProjectilePool();

    // Animator のパラメータ一覧（名前のハッシュ → 種類）。
    // animator.parameters は呼ぶたびに配列を新しく作るため、攻撃のたびに呼ぶと GC が発生する。初回だけ取得して使い回す。
    // ScriptableObject（Action）側に持つと全プレイヤーで共有されてしまうので、キャラクターごとのここに置く。
    private Dictionary<int, AnimatorControllerParameterType> _animatorParameters;
    // 一覧を作ったときの Controller。差し替えられたら作り直す。
    private RuntimeAnimatorController _cachedController;

    public SkillContext(CharacterMotor motor, Animator animator, AttackAnimationEvents animationEvents)
    {
        CharacterMotor = motor ?? throw new System.ArgumentNullException(nameof(motor));
        Animator = animator ?? throw new System.ArgumentNullException(nameof(animator));
        AnimationEvents = animationEvents ?? throw new System.ArgumentNullException(nameof(animationEvents));
    }

    /// <summary>
    /// Animator に、指定した名前と種類のパラメータがあるか確認する。Action の設定ミスを検出するために使う。
    /// </summary>
    public bool HasAnimatorParameter(string name, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(name) || Animator == null || Animator.runtimeAnimatorController == null)
            return false;

        if (_animatorParameters == null || _cachedController != Animator.runtimeAnimatorController)
        {
            // 無効な Animator は parameters が空を返すことがあり、それを覚えると以降すべて「無い」と判定してしまう。
            // 有効なときだけ一覧を作り、無効なら今回だけ「無い」と答える。
            if (!Animator.isActiveAndEnabled)
                return false;

            _animatorParameters = new Dictionary<int, AnimatorControllerParameterType>();
            foreach (AnimatorControllerParameter parameter in Animator.parameters)
                _animatorParameters[parameter.nameHash] = parameter.type;
            _cachedController = Animator.runtimeAnimatorController;
        }

        return _animatorParameters.TryGetValue(Animator.StringToHash(name), out AnimatorControllerParameterType actual)
            && actual == type;
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
        CharacterMotor.SetLocomotionLocked(false);
        CharacterMotor.EndPassThrough();
    }

    /// <summary>
    /// キャラクターの破棄時に、このコンテキストが持つリソース（飛び道具のプール）を片付ける。
    /// </summary>
    public void Dispose()
    {
        ProjectilePool.Dispose();
    }
}
