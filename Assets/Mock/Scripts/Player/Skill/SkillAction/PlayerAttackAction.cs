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
    /// この攻撃で与えるダメージ量。
    /// </summary>
    [SerializeField, Min(0)] private int _damage = 10;
    /// <summary>
    /// Trigger送信後、指定Stateに遷移するまでの最大待機時間。
    /// 単位は秒（実時間）。
    /// </summary>
    private const float STATEENTRYTIMEOUT = 1f;
    /// <summary>
    /// アニメーションの進行度が変化しない場合の最大許容時間。
    /// 単位は秒（実時間）。
    /// </summary>
    private const float NOPROGRESSTIMEOUT = 1f;

    public override async UniTask ExecuteAsync(SkillContext context, CancellationToken cancellationToken)
    {
        Animator animator = context.Animator;
        AttackAnimationEvents receiver = context.AnimationEvents;
        int stateHash = Animator.StringToHash(_stateFullPath);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAnimator(animator);
        if (!animator.HasState(0, stateHash))
            throw new InvalidOperationException($"Animator Stateが存在しません: {_stateFullPath}");

        bool hasTrigger = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == _triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
                hasTrigger = true;
        if (!hasTrigger)
            throw new InvalidOperationException($"Animator Triggerが存在しません: {_triggerName}");

        receiver.PrepareAttack(_damage);
        try
        {
            animator.SetTrigger(_triggerName);
            float entryStartedAt = Time.realtimeSinceStartup;
            AnimatorStateInfo stateInfo;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.fullPathHash == stateHash && !animator.IsInTransition(0))
                    break;
                if (Time.realtimeSinceStartup - entryStartedAt >= STATEENTRYTIMEOUT)
                    throw new TimeoutException($"State遷移失敗: {_stateFullPath}");
                await UniTask.NextFrame(cancellationToken);
            }

            // 直前に取得したアニメーションの進行度
            float lastNormalizedTime = stateInfo.normalizedTime;
            // 最後にアニメーションが進行した実時間
            float lastProgressAt = Time.realtimeSinceStartup;
            // AnimationEventの終了通知が届くまで待つ
            while (receiver.IsPlaying)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.fullPathHash != stateHash)
                    throw new InvalidOperationException($"終了イベント前に攻撃Stateを離れました: {_stateFullPath}");
                if (stateInfo.speed * stateInfo.speedMultiplier <= 0f)
                    throw new InvalidOperationException($"攻撃Stateが停止または逆再生されています: {_stateFullPath}");
                if (stateInfo.normalizedTime > 1.05f)
                    throw new TimeoutException($"攻撃終了イベントなし: {_stateFullPath}");
                if (stateInfo.normalizedTime > lastNormalizedTime + 0.000001f)
                {
                    lastNormalizedTime = stateInfo.normalizedTime;
                    lastProgressAt = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - lastProgressAt >= NOPROGRESSTIMEOUT)
                    throw new TimeoutException($"攻撃アニメーションの進行が停止しました: {_stateFullPath}");
                await UniTask.NextFrame(cancellationToken);
            }

            // AnimationEventの終了通知を受け取った時刻
            float finishedAt = Time.realtimeSinceStartup;
            // 攻撃StateがCurrent Stateでなくなるまで待機
            while (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateAnimator(animator);
                if (Time.realtimeSinceStartup - finishedAt >= STATEENTRYTIMEOUT)
                    throw new TimeoutException($"攻撃終了後にStateを離れませんでした: {_stateFullPath}");
                await UniTask.NextFrame(cancellationToken);
            }
        }
        finally
        {
            if (animator != null) animator.ResetTrigger(_triggerName);
            if (receiver != null) receiver.CancelAttack();
        }
    }

    /// <summary>
    /// Animatorが攻撃を実行できる状態か検証する。
    /// </summary>
    private static void ValidateAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || animator.speed <= 0f)
            throw new InvalidOperationException("攻撃Animatorが未設定、無効、または停止しています。");
    }
}
