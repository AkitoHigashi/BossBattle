using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 攻撃1発分。攻撃アニメーションを再生し、アニメーションイベントで当たり判定を開閉して、終わるまで待つ。
/// </summary>
/// <remarks>
/// ExecuteAsync の流れ：
/// 1. ValidateSetup … Animator に必要な State と Trigger があるか確認
/// 2. 攻撃内容を渡して Trigger を送る
/// 3. WaitForStateEntryAsync … 攻撃 State に入るのを待つ
/// 4. WaitForFinishEventAsync … アニメーションの「攻撃終了」イベントを待つ
/// 5. WaitForStateExitAsync … 攻撃 State を抜けるのを待つ（設定で省略可）
/// 6. Cleanup … 終了・中断・例外のどれでも、当たり判定を閉じて Animator を戻す
/// </remarks>
[CreateAssetMenu(fileName = "PlayerAttackAction", menuName = "Skills/PlayerAttackAction")]
public class PlayerAttackAction : SkillAction
{
    [Tooltip("攻撃アニメーションへ遷移する Trigger 名（Animator の Parameters に登録が必要）")]
    [SerializeField] private string _triggerName = "NormalAttack";
    [Tooltip("再生する攻撃 State のフルパス（例：Base Layer.NormalAttack）")]
    [SerializeField] private string _stateFullPath = "Base Layer.NormalAttack";
    [Tooltip("中断時に送る Trigger。空なら中断時に Animator を操作しない")]
    [SerializeField] private string _interruptTriggerName = "";
    [SerializeField, Min(0)] private int _damage = 10;
    [Tooltip("1回命中したときのブレイク値。「3連全ヒットで①」なら各段に 1/3 ずつ設定する")]
    [SerializeField, Min(0f)] private float _breakValue = 0f;
    [Tooltip("終了イベントの後、攻撃 State を抜けるまで待つか。" +
        "連続攻撃で次の攻撃 State へ直接つなぐ場合だけ OFF にする（待つと毎回立ち姿勢を挟んでガクつくため）")]
    [SerializeField] private bool _waitForStateExit = true;

    // 攻撃 State への進入、または終了後の退出を待つ最大時間
    private const float STATEENTRYTIMEOUT = 1f;
    // アニメーションの再生が進まないまま待つ最大時間
    private const float NOPROGRESSTIMEOUT = 1f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        execution.Token.ThrowIfCancellationRequested();
        int stateHash = ValidateSetup(context);

        Animator animator = context.Animator;
        AttackAnimationEvents receiver = context.AnimationEvents;
        CancellationToken token = execution.Token;
        bool completed = false;
        try
        {
            // 倍率は通常 1。溜め攻撃では ChargeAction が溜め結果に応じて設定する。
            // 同じスキルの踏み込みですでに当たった相手には当てない（BlinkAction の設定による）。
            receiver.PrepareAttack(
                execution.ApplyMultipliers(new AttackData(_damage, _breakValue)),
                execution.ExcludedHitTargets);
            // 前に中断した攻撃の中断用 Trigger が使われずに残っていると、この攻撃 State に入った直後に
            // その Trigger で抜けてしまう（終了イベント前に State を離れたエラーになる）。始める前に消しておく。
            ResetInterruptTrigger(context, animator);
            animator.SetTrigger(_triggerName);

            await WaitForStateEntryAsync(animator, stateHash, token);
            await WaitForFinishEventAsync(animator, receiver, stateHash, token);
            if (_waitForStateExit)
                await WaitForStateExitAsync(animator, stateHash, token);

            completed = true;
        }
        finally
        {
            Cleanup(context, receiver, animator, stateHash, completed);
        }
    }

    /// <summary>
    /// 攻撃を始める前に、Animator に必要な State と Trigger があるか確認する。攻撃 State のハッシュを返す。
    /// </summary>
    private int ValidateSetup(SkillContext context)
    {
        ValidateAnimator(context.Animator);
        if (context.AnimationEvents == null)
            throw new InvalidOperationException("AttackAnimationEventsが設定されていません。");

        int stateHash = Animator.StringToHash(_stateFullPath);
        if (!context.Animator.HasState(0, stateHash))
            throw new InvalidOperationException($"Animator Stateが存在しません: {_stateFullPath}");
        if (!context.HasAnimatorParameter(_triggerName, AnimatorControllerParameterType.Trigger))
            throw new InvalidOperationException($"Animator Triggerが存在しません: {_triggerName}");

        return stateHash;
    }

    /// <summary>
    /// 攻撃 State に入り、遷移も終わるまで待つ。一定時間で入れなければ失敗とする。
    /// </summary>
    private async UniTask WaitForStateEntryAsync(Animator animator, int stateHash, CancellationToken token)
    {
        float startedAt = Time.time;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            ValidateAnimator(animator);

            if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash && !animator.IsInTransition(0))
                return;
            if (Time.time - startedAt >= STATEENTRYTIMEOUT)
                throw new TimeoutException($"攻撃Stateへの遷移に失敗しました: {_stateFullPath}");

            await UniTask.NextFrame(token);
        }
    }

    /// <summary>
    /// アニメーションの「攻撃終了」イベント（OnAttackFinished）が届くまで待つ。
    /// </summary>
    /// <remarks>
    /// イベントの設定漏れやアニメーションの停止で永遠に待たないよう、異常を検出したら例外にする。
    /// </remarks>
    private async UniTask WaitForFinishEventAsync(
        Animator animator, AttackAnimationEvents receiver, int stateHash, CancellationToken token)
    {
        float lastNormalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        float lastProgressAt = Time.time;

        while (receiver.IsPlaying)
        {
            token.ThrowIfCancellationRequested();
            ValidateAnimator(animator);
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.fullPathHash != stateHash)
                throw new InvalidOperationException($"終了イベント前に攻撃Stateを離れました: {_stateFullPath}");
            if (stateInfo.speed * stateInfo.speedMultiplier <= 0f)
                throw new InvalidOperationException($"攻撃Stateが停止または逆再生されています: {_stateFullPath}");
            // 最後まで再生しても終了イベントが来ない（イベントの設定漏れ）
            if (stateInfo.normalizedTime > 1.05f)
                throw new TimeoutException($"攻撃終了イベントなし: {_stateFullPath}");

            if (stateInfo.normalizedTime > lastNormalizedTime + 0.000001f)
            {
                lastNormalizedTime = stateInfo.normalizedTime;
                lastProgressAt = Time.time;
            }
            if (Time.time - lastProgressAt >= NOPROGRESSTIMEOUT)
                throw new TimeoutException($"攻撃アニメーションの進行が停止しました: {_stateFullPath}");

            await UniTask.NextFrame(token);
        }
    }

    /// <summary>
    /// 攻撃 State を抜けるまで待つ。
    /// </summary>
    /// <remarks>
    /// 終了イベントの後も、Animator はまだ攻撃 State を再生していることがある。
    /// 抜ける前に次の攻撃を始めると、古い攻撃のイベントが次の攻撃に紛れ込むおそれがあるため待つ。
    /// </remarks>
    private async UniTask WaitForStateExitAsync(Animator animator, int stateHash, CancellationToken token)
    {
        float finishedAt = Time.time;
        while (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash)
        {
            token.ThrowIfCancellationRequested();
            ValidateAnimator(animator);
            if (Time.time - finishedAt >= STATEENTRYTIMEOUT)
                throw new TimeoutException($"攻撃終了後にStateを離れませんでした: {_stateFullPath}");

            await UniTask.NextFrame(token);
        }
    }

    /// <summary>
    /// 攻撃の後片付け。正常終了・中断・例外のどれでも呼ばれる。
    /// </summary>
    private void Cleanup(SkillContext context, AttackAnimationEvents receiver, Animator animator, int stateHash, bool completed)
    {
        receiver.CancelAttack();
        if (animator == null)
            return;

        // 使われなかった Trigger が残ると、後で勝手に攻撃が再生されるため消す。
        animator.ResetTrigger(_triggerName);

        // 処理を止めただけではアニメーションは止まらないので、中断時は立ち姿勢へ戻す。
        // 正常終了時は戻さない（State 退出を待たない設定だと、まだ攻撃 State にいて誤って中断してしまう）。
        if (!completed)
            TryRecoverAnimator(context, animator, stateHash);
    }

    /// <summary>
    /// 中断用 Trigger を送り、攻撃アニメーションから立ち姿勢へ戻す。
    /// Controller 側に、この Trigger で攻撃から退出する遷移の設定が必要。
    /// </summary>
    private void TryRecoverAnimator(SkillContext context, Animator animator, int attackHash)
    {
        if (string.IsNullOrWhiteSpace(_interruptTriggerName))
            return;

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool isInTransition = animator.IsInTransition(0);
        bool isEnteringAttack = isInTransition
            && animator.GetNextAnimatorStateInfo(0).fullPathHash == attackHash;
        // 攻撃へ入る途中なら中断する。攻撃から離れる途中なら、もう戻っているので何もしない。
        if (!isEnteringAttack && (current.fullPathHash != attackHash || isInTransition))
            return;

        if (context.HasAnimatorParameter(_interruptTriggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(_interruptTriggerName);
            return;
        }

        Debug.LogWarning($"中断用Animator Triggerが存在しません: {_interruptTriggerName}");
    }

    private void ResetInterruptTrigger(SkillContext context, Animator animator)
    {
        if (string.IsNullOrWhiteSpace(_interruptTriggerName))
            return;
        if (context.HasAnimatorParameter(_interruptTriggerName, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(_interruptTriggerName);
    }

    private static void ValidateAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("攻撃Animatorが未設定、無効、または停止しています。");
    }
}
