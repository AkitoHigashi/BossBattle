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

                if (stateInfo.fullPathHash == stateHash && !animator.IsInTransition(0))
                {
                    break;
                }
                if (Time.time - entryStartedAt >= STATEENTRYTIMEOUT + TIMEOUTMARGIN)
                {
                    Debug.LogWarning($"AnimatorStateの遷移がタイムアウトしました。{_stateFullPath}");
                    break;
                }
                await UniTask.NextFrame(cancellationToken);

                //実際のState再生時間を取得
                float duration = stateInfo.length;

                if (duration <= 0f)
                {
                    throw new InvalidOperationException($"{_stateFullPath}のAnimatorStateの再生時間が0以下です。");
                }

                float timeoutAt = Time.time + duration + TIMEOUTMARGIN;

                while (receiver.IsPlaying)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Time.time >= timeoutAt)
                    {
                        Debug.LogWarning($"攻撃アニメーションの再生がタイムアウトしました。{_stateFullPath}");
                        break;
                    }
                    // 終了イベントなしで他Stateへ移った場合は異常
                    stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                    if (stateInfo.fullPathHash != stateHash)
                    {
                        throw new InvalidOperationException(
                            $"{_stateFullPath}: 攻撃が途中で中断されました。"
                        );
                    }

                    await UniTask.NextFrame(cancellationToken);
                }
            }
        }
        finally
        {
            animator.ResetTrigger(_triggerName);
            receiver.CancelAttack();
        }
    }
}

