using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAttackAction", menuName = "Skills/PlayerAttackAction")]
public class PlayerAttackAction : SkillAction
{
    /// <summary>
    /// animatorのステート名。
    /// </summary>
    [SerializeField] private string _triggerName = "NormalAttack";
    [SerializeField] private string _stateFullPath = "Base Layer.NormalAttack";
    /// <summary>
    /// 攻撃のダメージ量。
    /// </summary>
    [SerializeField, Min(0)] private int _damage = 10;
    // 全攻撃で共通の安全用タイムアウト設定
    private const float STATEENTRYTIMEOUT = 1.0f;
    private const float TIMEOUTMARGIN = 0.5f;

    public override async UniTask ExecuteAsync(SkillContext context, CancellationToken cancellationToken)
    {
        Animator animator = context.Animator;
        AttackAnimationEvents receiver = context.AnimationEvents;
        int stateHash = Animator.StringToHash(_stateFullPath);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"AnimatorStateが存在しません。{_stateFullPath}");
        }
        receiver.PrepareAttack(_damage);
        try
        {
            // 攻撃アニメーションへの遷移を要求
            animator.SetTrigger(_triggerName);
            //Stateへの線移管料を待つ
            float entryStartedAt = Time.time;
            AnimatorStateInfo stateInfo;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                if (stateInfo.fullPathHash == stateHash &&
                    !animator.IsInTransition(0))
                {
                    break;
                }

                if (Time.time - entryStartedAt >= STATEENTRYTIMEOUT)
                {
                    throw new TimeoutException(
                        $"State遷移失敗: {_stateFullPath}"
                    );
                }

                await UniTask.NextFrame(cancellationToken);
            }

            // Stateに入ったら再生時間を取得
            float duration = stateInfo.length;
            float timeoutAt = Time.time + duration + TIMEOUTMARGIN;

            // AnimationEventによる攻撃終了を待つ
            while (receiver.IsPlaying)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Time.time >= timeoutAt)
                {
                    throw new TimeoutException(
                        $"攻撃終了イベントなし: {_stateFullPath}"
                    );
                }

                await UniTask.NextFrame(cancellationToken);
            }
        }
        finally
        {
            animator.ResetTrigger(_triggerName);
            receiver.CancelAttack();
        }
    }
}

