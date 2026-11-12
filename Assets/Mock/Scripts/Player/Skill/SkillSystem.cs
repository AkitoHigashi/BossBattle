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

    // 連撃の入力待ち中に回避などで割り込まれたとき、続きから再開するための情報。再開できるのは1つだけ。
    private SkillDefinition _resumeSkill;
    private bool _disposed;
    // 割り込みで前のスキルの後片付けを待つ間、新しい発動や入力が入り込むのを防ぐ。
    private bool _isInterrupting;
    // 割り込みで中断された連撃の、続きから再開できる段。再開できない状態なら -1。
    private int _resumeStage;
    // 再開できる期限（Time.time）。期限を過ぎたら、再開はできずクールダウンに入る。
    private float _resumeDeadline;
    // 再開できる期限の長さ。UI のバー表示用。
    private float _resumeWindowDuration;

    public SkillSystem(SkillContext skillContext)
    {
        _skillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary>
    /// スキルを発動する。クールダウン中・実行中・割り込み処理中なら発動せず false。
    /// </summary>
    public bool TryUseSkill(SkillDefinition definition, Vector3 direction)
    {
        if (_isExecuting.Value || _isInterrupting)
            return false;
        // 回避で中断した連撃の続きなら、クールダウンに関係なく続きの段から始める。
        if (TryResumeSkill(definition, direction))
            return true;
        if (!CanUseSkill(definition))
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
            // 割り込みでの中断だと記録する。連撃の入力待ち中なら、後で続きから再開できるようにするため。
            oldExecution.MarkInterrupted();
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
        ExpireResumeIfNeeded();
        if (definition == null || !_nextUsableTimes.TryGetValue(definition, out float nextUsableTime))
            return 0f;

        return Mathf.Max(0f, nextUsableTime - Time.time);
    }

    /// <summary>
    /// 指定したスキルが追加入力を受け付けている残り時間（連撃の猶予など）。受付中でなければ false。
    /// 回避で中断した連撃の、再開できる残り時間も含む。
    /// </summary>
    public bool TryGetInputWindow(SkillDefinition definition, out float remaining, out float duration)
    {
        remaining = 0f;
        duration = 0f;
        if (definition == null)
            return false;

        ExpireResumeIfNeeded();
        if (_resumeSkill == definition)
        {
            remaining = Mathf.Max(0f, _resumeDeadline - Time.time);
            duration = _resumeWindowDuration;
            return duration > 0f;
        }

        if (_currentExecution == null || _currentExecution.Definition != definition)
            return false;
        remaining = _currentExecution.InputWindowRemaining;
        duration = _currentExecution.InputWindowDuration;
        return duration > 0f;
    }


    private void StartSkill(SkillDefinition definition, Vector3 direction, int startStage = 0)
    {
        // 中断した連撃の再開待ちは、他のスキルを使っても消さない。受付時間が切れるまで、押せば続きの段が出る。
        var execution = new SkillExecution(definition, direction, startStage);
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
            Debug.Log($"スキルの実行がキャンセルされました。{execution.Definition.SkillName}");
        }
        catch (Exception ex)
        {
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
        // 連撃の入力待ち中に割り込まれたなら、クールダウンにせず、猶予内に押せば続きから再開できるようにする。
        if (CanKeepForResume(execution))
            SaveResume(execution);
        // 終了時開始の設定なら、中断やエラーで終わった場合もここからクールダウンを始める。
        else if (execution.Definition.CooldownPolicy == CooldownStartPolicy.OnEnd)
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
        StartCooldownFrom(definition, Time.time);
    }

    private void StartCooldownFrom(SkillDefinition definition, float startTime)
    {
        _nextUsableTimes[definition] = startTime + definition.Cooldown;
    }

    private bool CanKeepForResume(SkillExecution execution)
    {
        return !_disposed
            && execution.WasInterrupted
            && execution.ResumeStage >= 0
            && Time.time < execution.ResumeDeadline;
    }

    private void SaveResume(SkillExecution execution)
    {
        // 再開待ちは1つだけ持つ。別のスキルの再開待ちが残っていたら、クールダウンなしで消えないよう今から始める。
        if (_resumeSkill != null && _resumeSkill != execution.Definition)
            EndResume(Time.time);

        _resumeSkill = execution.Definition;
        _resumeStage = execution.ResumeStage;
        _resumeDeadline = execution.ResumeDeadline;
        _resumeWindowDuration = execution.ResumeWindowDuration;
    }

    /// <summary>
    /// 中断した連撃の続きを始める。再開できるスキルでなければ false。
    /// </summary>
    private bool TryResumeSkill(SkillDefinition definition, Vector3 direction)
    {
        ExpireResumeIfNeeded();
        if (_disposed || definition == null || definition != _resumeSkill)
            return false;

        int stage = _resumeStage;
        _resumeSkill = null;
        StartSkill(definition, direction, stage);
        return true;
    }

    /// <summary>
    /// 再開の期限が過ぎていたら、期限の時点で連撃が終わったものとしてクールダウンを始める。
    /// </summary>
    /// <remarks>
    /// 毎フレーム監視する代わりに、状態を読むとき（UI の表示・発動の判定）に確認する。
    /// クールダウンの開始時刻を期限に合わせるので、確認が遅れても終了時刻はずれない。
    /// </remarks>
    private void ExpireResumeIfNeeded()
    {
        if (_resumeSkill != null && Time.time >= _resumeDeadline)
            EndResume(_resumeDeadline);
    }

    /// <summary>
    /// 再開をあきらめ、指定した時刻からクールダウンを始める（終了時開始のスキルの場合）。
    /// </summary>
    private void EndResume(float cooldownStartTime)
    {
        SkillDefinition skill = _resumeSkill;
        _resumeSkill = null;
        if (skill != null && skill.CooldownPolicy == CooldownStartPolicy.OnEnd)
            StartCooldownFrom(skill, cooldownStartTime);
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
        ExpireResumeIfNeeded();
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
