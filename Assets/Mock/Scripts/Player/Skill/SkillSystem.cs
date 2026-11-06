using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// スキルの発動・実行・中断を管理する（全スキル共通）。個々のスキルの中身は SkillAction が持つ。
/// </summary>
/// <remarks>
/// 読む順番：
/// 1. 発動：TryUseSkill / HandleSkillInput / TryUseSkillAsync（割り込み）
/// 2. 実行：StartSkill → ExecuteAsync（Actions を順に実行）→ FinishExecution（後片付け）
/// 3. 中断・破棄：CancelActiveSkill / Dispose
/// </remarks>
public class SkillSystem : ISkillSystem, IDisposable
{
    /// <summary>スキルを実行中かどうかの通知。</summary>
    public Observable<bool> IsExecuting => _isExecuting;
    /// <summary>実行中のスキル。実行していなければ null。UI などが読み取り専用で使う。</summary>
    public SkillDefinition CurrentSkill => _currentExecution?.Definition;

    private readonly SkillContext _skillContext;
    private readonly Dictionary<SkillDefinition, float> _nextUsableTimes = new Dictionary<SkillDefinition, float>();
    private readonly ReactiveProperty<bool> _isExecuting = new ReactiveProperty<bool>(false);
    private SkillExecution _currentExecution;
    private bool _disposed;
    // 割り込みで前のスキルの後片付けを待つ間、新しい発動や入力が入り込むのを防ぐ。
    private bool _isInterrupting;

    public SkillSystem(SkillContext skillContext)
    {
        _skillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary>
    /// スキルを発動する。クールダウン中・実行中・割り込み処理中なら発動せず false。
    /// </summary>
    public bool TryUseSkill(SkillDefinition definition, Vector3 direction)
    {
        if (!CanUseSkill(definition) || _isExecuting.Value || _isInterrupting)
            return false;

        StartSkill(definition, direction);
        return true;
    }

    /// <summary>
    /// 押下・解放の入力を処理する。
    /// 同じスキルを実行中ならその入力として記録し、何も実行していなければ押下を発動要求として扱う。
    /// </summary>
    public bool HandleSkillInput(SkillDefinition definition, SkillInputPhase phase, Vector3 direction)
    {
        if (_disposed || definition == null || _isInterrupting)
            return false;

        if (_currentExecution == null)
            return phase == SkillInputPhase.Pressed && TryUseSkill(definition, direction);

        // 別のスキルを実行中なら受け付けない（割り込みは TryUseSkillAsync が扱う）。
        if (_currentExecution.Definition != definition)
            return false;

        // 同じスキルへの入力は再発動せず、押下状態・押下回数に反映する（連撃・長押し用）。
        _currentExecution.ReceiveInput(phase);
        return true;
    }

    /// <summary>
    /// 割り込みつきの発動（回避など）。割り込めるなら、前のスキルの後片付けが終わるのを待ってから発動する。
    /// </summary>
    public async UniTask<bool> TryUseSkillAsync(SkillDefinition definition, Vector3 direction, CancellationToken requestToken)
    {
        // 発動要求元が無効化されていたら、実行中のスキルにも触れない。
        if (requestToken.IsCancellationRequested || _isInterrupting || !CanUseSkill(definition))
            return false;

        if (_currentExecution == null)
            return TryUseSkill(definition, direction);

        if (!CanInterrupt(definition, _currentExecution))
            return false;

        return await InterruptAndStartAsync(definition, direction, requestToken);
    }

    /// <summary>
    /// 実行中のスキルを中断し、後片付けの完了を待ってから次のスキルを始める。
    /// </summary>
    private async UniTask<bool> InterruptAndStartAsync(SkillDefinition definition, Vector3 direction, CancellationToken requestToken)
    {
        _isInterrupting = true;
        try
        {
            // Cancel は中断の要求だけ。当たり判定や移動の解除まで終わるのを Completion で待つ。
            SkillExecution oldExecution = _currentExecution;
            oldExecution.Cancel();
            await oldExecution.Completion;

            // 待っている間に要求元が無効化されたら、遅れて発動させない。
            if (requestToken.IsCancellationRequested)
                return false;
            // 待っている間に状態が変わり得るので、発動できるか確認し直す。
            if (!CanUseSkill(definition) || _currentExecution != null || _isExecuting.Value)
                return false;

            StartSkill(definition, direction);
            return true;
        }
        finally
        {
            // 発動・途中の return・例外のどれでも、入力の受付を再開する。
            _isInterrupting = false;
        }
    }


    public void UpdateAimDirection(Vector3 direction)
    {
        if (_disposed)
            return;

        _currentExecution?.SetAimDirection(direction);
    }

    /// <summary>
    /// クールダウンの残り秒数。クールダウン中でなければ 0。
    /// </summary>
    public float GetCooldownRemaing(SkillDefinition definition)
    {
        if (definition == null || !_nextUsableTimes.TryGetValue(definition, out float nextUsableTime))
            return 0f;

        return Mathf.Max(0f, nextUsableTime - Time.time);
    }

    /// <summary>
    /// 実行中のスキルが追加入力を受け付けている残り時間（連撃の猶予など）。受付中でなければ false。
    /// </summary>
    public bool TryGetInputWindow(out float remaining, out float duration)
    {
        remaining = _currentExecution?.InputWindowRemaining ?? 0f;
        duration = _currentExecution?.InputWindowDuration ?? 0f;
        return duration > 0f;
    }


    private void StartSkill(SkillDefinition definition, Vector3 direction)
    {
        var execution = new SkillExecution(definition, direction);
        // 発動のきっかけになった押下も、この実行の1回目の押下として数える。
        execution.ReceiveInput(SkillInputPhase.Pressed);
        _currentExecution = execution;

        if (definition.CooldownPolicy == CooldownStartPolicy.OnStart)
            StartCooldown(definition);
        // 歩けないスキルの間は通常移動を止める。解除は終了時の AbortActiveAction で行う。
        _skillContext.CharacterMotor.SetLocomotionLocked(!definition.CanMoveDuringSkill);
        _isExecuting.Value = true;

        ExecuteAsync(execution).Forget();
    }

    /// <summary>
    /// スキルを実行し、どう終わっても後片付けする。
    /// </summary>
    private async UniTask ExecuteAsync(SkillExecution execution)
    {
        try
        {
            await RunActionsAsync(execution);
        }
        catch (OperationCanceledException)
        {
            // 回避による割り込みや無効化での中断は、通常の終了として扱う。
            Debug.Log($"スキルの実行がキャンセルされました。{execution.Definition.SkillName}");
        }
        catch (Exception ex)
        {
            // 設定ミスなど予期しない失敗は、どのスキルで起きたか分かるよう記録する。
            Debug.LogError($"スキルの実行中にエラーが発生しました。{execution.Definition.SkillName} {ex}");
        }
        finally
        {
            FinishExecution(execution);
        }
    }

    /// <summary>
    /// SkillDefinition の Actions を上から順に1つずつ実行する（例：移動が終わってから斬撃）。
    /// </summary>
    private async UniTask RunActionsAsync(SkillExecution execution)
    {
        foreach (SkillAction action in execution.Definition.Actions)
        {
            // Action の切り替わりでも中断を確認し、中断後に次の Action が始まらないようにする。
            execution.Token.ThrowIfCancellationRequested();
            if (action == null)
                throw new InvalidOperationException($"スキルが設定されていない   {execution.Definition.SkillName}");

            await action.ExecuteAsync(_skillContext, execution);
        }
    }

    /// <summary>
    /// 実行の後片付け。正常終了・中断・エラーのどれでも呼ばれる。
    /// </summary>
    /// <remarks>
    /// finally を重ねているのは、ある段階で例外が出ても次の段階を必ず行うため。
    /// 特に最後の NotifyCompleted が漏れると、割り込み側（InterruptAndStartAsync）が待ち続けてしまう。
    /// </remarks>
    private void FinishExecution(SkillExecution execution)
    {
        try
        {
            // 1. 当たり判定・強制移動・歩き停止など、Action が残した状態を解除する。
            _skillContext.AbortActiveAction();
        }
        finally
        {
            try
            {
                // 2. クールダウンを始め、実行中の状態を解除する。
                ReleaseExecution(execution);
            }
            finally
            {
                // 3. この実行が終わったことを通知する（成功の意味ではなく「終わった」の意味）。
                execution.NotifyCompleted();
            }
        }
    }

    private void ReleaseExecution(SkillExecution execution)
    {
        // 終了時開始の設定なら、中断やエラーで終わった場合もここからクールダウンを始める。
        if (execution.Definition.CooldownPolicy == CooldownStartPolicy.OnEnd)
            StartCooldown(execution.Definition);
        execution.Dispose();

        // まだ「現在の実行」のときだけ解除する。割り込みで次の実行が始まっていたら、その状態は消さない。
        if (!ReferenceEquals(_currentExecution, execution))
            return;

        _currentExecution = null;
        // 破棄後は、破棄済みの通知用プロパティを触らない。
        if (!_disposed)
            _isExecuting.Value = false;
    }

    private void StartCooldown(SkillDefinition definition)
    {
        _nextUsableTimes[definition] = Time.time + definition.Cooldown;
    }

    /// <summary>
    /// 実行中のスキルを中断する（キャラクターの無効化時など）。
    /// </summary>
    public void CancelActiveSkill()
    {
        if (_disposed)
            return;

        try
        {
            // 中断を要求する。実行状態の解除・完了通知は FinishExecution が行う。
            _currentExecution?.Cancel();
        }
        finally
        {
            // 実行側が中断に気づくのを待たず、残っている判定や移動の上書きをすぐ解除する。
            _skillContext.AbortActiveAction();
        }
    }

    /// <summary>
    /// 新しい発動を止め、実行中のスキルを中断して後片付けする。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        // 中断処理が動く前に破棄済みにして、新しい発動や終了時の通知を止める。
        _disposed = true;
        try
        {
            _currentExecution?.Cancel();
        }
        finally
        {
            _skillContext.AbortActiveAction();
            _isExecuting.Dispose();
        }
    }

    /// <summary>
    /// スキルの設定とクールダウンから、発動できるか判定する。実行中かどうかは呼び出し元が判断する。
    /// </summary>
    private bool CanUseSkill(SkillDefinition definition)
    {
        if (_disposed || definition == null || definition.Actions == null || definition.Actions.Length == 0)
            return false;

        return !_nextUsableTimes.TryGetValue(definition, out float nextUsableTime) || Time.time >= nextUsableTime;
    }

    /// <summary>
    /// 次のスキルの「割り込める」と、今のスキルの「中断されてもよい」が両方そろえば割り込める。
    /// </summary>
    private bool CanInterrupt(SkillDefinition nextSkill, SkillExecution currentExecution)
    {
        if (nextSkill == null || currentExecution == null)
            return false;

        return nextSkill.CanInterruptOthers && currentExecution.Definition.CanBeInterrupted;
    }
}
