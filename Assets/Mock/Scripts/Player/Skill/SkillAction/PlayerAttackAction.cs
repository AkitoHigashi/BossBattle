using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAttackAction", menuName = "Skills/PlayerAttackAction")]
public class PlayerAttackAction : SkillAction
{
    /// <summary>
    /// 攻撃アニメーションへの遷移に使用するTrigger名。
    /// Animator ControllerのParametersに登録されている必要がある。
    /// </summary>
    [SerializeField] private string _triggerName = "NormalAttack";
    /// <summary>
    /// 再生する攻撃Stateのフルパス。
    /// 例：Base Layer.NormalAttack
    /// </summary>
    [SerializeField] private string _stateFullPath = "Base Layer.NormalAttack";
    /// <summary>
    /// 攻撃が中断された際に使用するTrigger。
    /// 空文字の場合は中断時のState遷移を要求しない。
    /// </summary>
    [SerializeField] private string _interruptTriggerName = "";
    /// <summary>
    /// この攻撃で与えるダメージ量。
    /// </summary>
    [SerializeField, Min(0)] private int _damage = 10;
    /// <summary>
    /// アニメーションの再生進行度が変化しない場合、異常停止と判断するまでの時間。
    /// </summary>
    private const float STATEENTRYTIMEOUT = 1f;
    /// <summary>
    /// 再生進行度が変化しない場合の最大許容時間
    /// </summary>
    private const float NOPROGRESSTIMEOUT = 1f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        Animator animator = context.Animator;
        AttackAnimationEvents receiver = context.AnimationEvents;
        execution.Token.ThrowIfCancellationRequested();
        ValidateAnimator(animator);

        if (receiver == null)
            throw new InvalidOperationException("AttackAnimationEventsが設定されていません。");

        int stateHash = Animator.StringToHash(_stateFullPath);

        if (!animator.HasState(0, stateHash))
            throw new InvalidOperationException($"Animator Stateが存在しません: {_stateFullPath}");
        // 指定したTriggerがAnimatorに登録されているか確認
        bool hasTrigger = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == _triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
                hasTrigger = true;
        if (!hasTrigger)
            throw new InvalidOperationException($"Animator Triggerが存在しません: {_triggerName}");

        try
        {
            receiver.PrepareAttack(_damage);
            animator.SetTrigger(_triggerName);
            float entryStartedAt = Time.time;
            AnimatorStateInfo stateInfo;
            while (true)
            {
                execution.Token.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                // 目的のStateに入り、Transitionも完了していれば終了
                if (stateInfo.fullPathHash == stateHash && !animator.IsInTransition(0))
                    break;
                // 指定時間以内に遷移できなければ異常終了
                if (Time.time - entryStartedAt >= STATEENTRYTIMEOUT)
                    throw new TimeoutException($"攻撃Stateへの遷移に失敗しました: {_stateFullPath}");
                await UniTask.NextFrame(execution.Token);
            }

            // 現在のアニメーション再生進行度
            float lastNormalizedTime = stateInfo.normalizedTime;
            // 最後にアニメーションが進んだゲーム内時刻
            float lastProgressAt = Time.time;
            // AnimationEventの終了通知が届くまで待つ
            while (receiver.IsPlaying)
            {
                execution.Token.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                // 終了イベント前に別Stateへ移動していたら異常
                if (stateInfo.fullPathHash != stateHash)
                    throw new InvalidOperationException($"終了イベント前に攻撃Stateを離れました: {_stateFullPath}");
                if (stateInfo.speed * stateInfo.speedMultiplier <= 0f)
                    throw new InvalidOperationException($"攻撃Stateが停止または逆再生されています: {_stateFullPath}");
                // 再生進行度が105%を超えても終了イベントがない
                if (stateInfo.normalizedTime > 1.05f)
                    throw new TimeoutException($"攻撃終了イベントなし: {_stateFullPath}");
                if (stateInfo.normalizedTime > lastNormalizedTime + 0.000001f)
                {
                    lastNormalizedTime = stateInfo.normalizedTime;
                    lastProgressAt = Time.time;
                }
                // 一定時間アニメーションが進まなかった場合
                if (Time.time - lastProgressAt >= NOPROGRESSTIMEOUT)
                    throw new TimeoutException($"攻撃アニメーションの進行が停止しました: {_stateFullPath}");
                await UniTask.NextFrame(execution.Token);
            }

            // AnimationEventの終了通知を受け取った時刻
            float finishedAt = Time.time;
            // 攻撃StateがCurrent Stateでなくなるまで待機
            while (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash)
            {
                execution.Token.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                if (Time.time - finishedAt >= STATEENTRYTIMEOUT)
                    throw new TimeoutException($"攻撃終了後にStateを離れませんでした: {_stateFullPath}");
                await UniTask.NextFrame(execution.Token);
            }
        }
        finally
        {
            // 攻撃判定を解除する
            receiver.CancelAttack();
            if (animator != null)
            {
                // 未消費のTriggerを解除
                animator.ResetTrigger(_triggerName);
                TryRecoverAnimator(animator, stateHash);
            }
        }
    }

    /// <summary>
    /// 攻撃が中断された場合、設定されている中断Triggerを送信する。
    /// 中断Triggerが未設定の場合はAnimatorを操作しない。
    /// </summary>
    private void TryRecoverAnimator(Animator animator, int attackHash)
    {
        // 中断先のTriggerが設定されていなければ何もしない
        if (string.IsNullOrWhiteSpace(_interruptTriggerName))
            return;

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool isInTransition = animator.IsInTransition(0);
        bool isEnteringAttack = isInTransition
            && animator.GetNextAnimatorStateInfo(0).fullPathHash == attackHash;
        // 攻撃への遷移中も中断する。攻撃から離れる遷移には介入しない。
        if (!isEnteringAttack && (current.fullPathHash != attackHash || isInTransition))
            return;

        // 中断用Triggerが存在するか確認
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == _interruptTriggerName
                    && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                // 指定したTriggerで中断時の遷移を要求する
                animator.SetTrigger(_interruptTriggerName);
                return;
            }
        }

        Debug.LogWarning($"中断用Animator Triggerが存在しません: {_interruptTriggerName}");
    }

    /// <summary>
    /// Animatorが攻撃を実行できる状態か検証する。
    /// </summary>
    private static void ValidateAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("攻撃Animatorが未設定、無効、または停止しています。");
    }
}
