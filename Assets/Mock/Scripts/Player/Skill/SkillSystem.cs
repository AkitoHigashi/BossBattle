using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class SkillSystem : ISkillSystem, IDisposable
{
    /// <summary>
    /// 現在スキルが実行中かどうかを外部へ通知する
    /// </summary>
    public Observable<bool> IsExecuting => _isExecuting;
    private readonly SkillContext _skillContext;
    private readonly Dictionary<SkillDefinition, float> _nextUsableTimes = new Dictionary<SkillDefinition, float>();
    private readonly ReactiveProperty<bool> _isExecuting = new ReactiveProperty<bool>(false);
    private SkillExecution _currentExecution;
    private bool _disposed;
    // 中断の後片付けを待つ間、新しい発動要求や追加入力が入り込むのを防ぐ。
    private bool _isInterrupting;

    public SkillSystem(SkillContext skillContext)
    {
        _skillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary>
    /// 使用条件に加え、現在ほかのスキルを実行していないか
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    public bool TryUseSkill(SkillDefinition definition, Vector3 direction)
    {
        if (!CanUseSkill(definition) || _isExecuting.Value || _isInterrupting)
            return false;
        StartSkill(definition, direction);
        return true;
    }

    public void UpdateAimDirection(Vector3 direction)
    {
        if (_disposed)
            return;

        _currentExecution?.SetAimDirection(direction);
    }

    /// <summary>
    /// スキルの入力を処理する。
    /// 実行中なら現在のSkillExecutionへ入力を通知
    /// 未実行ならPressedを新規発動要求として扱う。
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="phase"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public bool HandleSkillInput(SkillDefinition definition, SkillInputPhase phase, Vector3 direction)
    {
        if (_disposed || definition == null || _isInterrupting)
            return false;

        if (_currentExecution != null)
        {
            // 同じスキルへの入力は再発動せず、実行中の押下状態・押下回数に反映する。
            // 別スキルによる割り込みは TryUseSkillAsync で扱う。
            if (_currentExecution.Definition == definition)
                _currentExecution.ReceiveInput(phase, direction);
            else
                return false;
        }
        else
        {
            // Pressedの場合は新規発動を試みる
            if (phase == SkillInputPhase.Released)
                return false;

            return TryUseSkill(definition, direction);
        }

        return true;
    }

    /// <summary>
    /// クールダウンの残り時間を取得します。
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    public float GetCooldownRemaing(SkillDefinition definition)
    {
        if (definition == null || !_nextUsableTimes.TryGetValue(definition, out float nextUsableTime))
        {
            return 0f;
        }
        return Mathf.Max(0f, nextUsableTime - Time.time);
    }

    /// <summary>
    /// 割り込みが許可されていれば、現在のスキルの後片付け完了を待って次を開始する。
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public async UniTask<bool> TryUseSkillAsync(SkillDefinition definition, Vector3 direction, CancellationToken requestToken)
    {
        // 発動要求元が無効化されていたら、実行中のスキルにも触れない。
        if (requestToken.IsCancellationRequested)
            return false;
        if (_isInterrupting || !CanUseSkill(definition))
            return false;

        if (_currentExecution == null)
        {
            return TryUseSkill(definition, direction);
        }
        else if (CanInterrupt(definition, _currentExecution) && !_isInterrupting)
        {
            try
            {
                _isInterrupting = true;
                SkillExecution oldExecution = _currentExecution;
                // Cancel は中断要求のみ。判定・移動の解除や実行状態の更新まで待ってから次へ進む。
                oldExecution.Cancel();
                await oldExecution.Completion;
                // 待機中に要求元が無効化された場合、遅れて次のスキルを発動させない。
                if (requestToken.IsCancellationRequested)
                    return false;
                // await を挟むと状態が変わり得るため、使用条件と実行状態を再確認する。
                if (!CanUseSkill(definition) || _currentExecution != null || _isExecuting.Value)
                    return false;

                StartSkill(definition, direction);
                return true;
            }
            finally
            {
                // 発動成功・途中の return・例外のいずれでも、割り込み中の入力制限を解除する。
                _isInterrupting = false;
            }
        }
        else
            return false;
    }

    /// <summary>
    /// スキルの実行を開始し、必要に応じてクールダウンを設定します。
    /// </summary>
    /// <param name="definition">実行するスキルの定義。</param>
    /// <param name="direction">スキルの発動方向。</param>
    private void StartSkill(SkillDefinition definition, Vector3 direction)
    {
        var execution = new SkillExecution(definition, direction);
        // 発動のきっかけとなった最初の押下も、この実行の入力として数える。
        execution.ReceiveInput(SkillInputPhase.Pressed, direction);
        _currentExecution = execution;
        if (definition.CooldownPolicy == CooldownStartPolicy.OnStart)
            _nextUsableTimes[definition] = Time.time + definition.Cooldown;
        _isExecuting.Value = true;
        ExecutAsync(execution).Forget();
    }

    /// <summary>
    /// スキルの設定・Cooldownなどの使用条件
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    private bool CanUseSkill(SkillDefinition definition)
    {
        // 破棄済み、定義なし、実行する Action なしの場合は使用不可。
        // 実行中かどうかは呼び出し元で判断する（割り込み可否と区別するため）。
        if (_disposed || definition == null || definition.Actions == null || definition.Actions.Length == 0)
            return false;

        //クールダウン中なら使用不可。
        if (_nextUsableTimes.TryGetValue(definition, out float nextUsableTime) && Time.time < nextUsableTime)
            return false;


        return true;
    }

    /// <summary>
    /// 割り込みが可能か。
    /// </summary>
    /// <param name="nextSkill"></param>
    /// <param name="currentExecution"></param>
    /// <returns></returns>
    private bool CanInterrupt(SkillDefinition nextSkill, SkillExecution currentExecution)
    {
        if (nextSkill == null || currentExecution == null)
            return false;

        // 次のスキルの「割り込める」と、現在のスキルの「中断されてもよい」の両方が必要。
        return nextSkill.CanInterruptOthers && currentExecution.Definition.CanBeInterrupted;
    }

    /// <summary>
    /// スキルの実行を非同期で行います。
    /// </summary>
    /// <returns></returns>
    private async UniTask ExecutAsync(SkillExecution execution)
    {
        try
        {
            // Action は定義された順に1つずつ実行する。例：移動が完了してから斬撃を開始する。
            foreach (var action in execution.Definition.Actions)
            {
                // Action の切り替わりでも中断を確認し、次の Action が始まるのを防ぐ。
                execution.Token.ThrowIfCancellationRequested();
                if (action == null)
                {
                    throw new InvalidOperationException($"スキルが設定されていない   {execution.Definition.SkillName}");
                }
                await action.ExecuteAsync(_skillContext, execution);
            }
        }
        catch (OperationCanceledException)
        {
            // 回避による割り込みや無効化に伴う中断は、通常の終了経路として扱う。
            Debug.Log($"スキルの実行がキャンセルされました。{execution.Definition.SkillName}");
        }
        catch (Exception ex)
        {
            // 設定不備などの予期しない失敗は、対象スキルと例外を記録して調査できるようにする。
            Debug.LogError($"スキルの実行中にエラーが発生しました。{execution.Definition.SkillName} {ex}");
        }
        finally
        {
            // 正常終了・中断・エラーのすべてで後片付けする。
            // finally を重ねることで、ある段階で例外が出ても次の後片付けへ進む。
            try
            {
                // 攻撃判定や移動の上書きなど、Action が残した状態を解除する。
                _skillContext.AbortActiveAction();
            }
            finally
            {
                try
                {
                    // 終了時開始の設定なら、中断やエラーの場合もここからクールダウンを計算する。
                    if (execution.Definition.CooldownPolicy == CooldownStartPolicy.OnEnd)
                        _nextUsableTimes[execution.Definition] = Time.time + execution.Definition.Cooldown;
                    execution.Dispose();
                    // この実行がまだ現在の実行である場合だけ解除し、別の実行の状態を消さない。
                    if (ReferenceEquals(_currentExecution, execution))
                    {
                        _currentExecution = null;

                        // SkillSystem の破棄後は、破棄済みの通知用プロパティを更新しない。
                        if (!_disposed)
                            _isExecuting.Value = false;
                    }
                }
                finally
                {
                    // 後片付けで例外が出ても完了を通知し、割り込み側の Completion 待機を終わらせる。
                    // 完了通知は成功を表すものではなく、この実行の終了を表す。
                    execution.NotifyCompleted();
                }
            }
        }
    }

    /// <summary>
    /// スキルの実行をキャンセルします。
    /// </summary>
    public void CancelActiveSkill()
    {
        if (_disposed)
            return;

        try
        {
            // 非同期実行へ中断を要求する。実行状態の解除・完了通知は ExecutAsync 側が担当する。
            _currentExecution?.Cancel();
        }
        finally
        {
            // 実行側が中断を検知するのを待たず、残っている判定や移動の上書きを解除する。
            _skillContext.AbortActiveAction();
        }
    }

    /// <summary>
    /// 新規発動を禁止し、実行中のスキルの中断と共有状態・通知リソースの後片付けを行う。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        // 中断に伴う処理が動く前に破棄済みにし、新規発動や終了時の実行状態通知を抑止する。
        _disposed = true;
        try
        {
            _currentExecution?.Cancel();
        }
        finally
        {
            // 中断要求中に例外が出た場合も、共有状態の解除を試みる。
            _skillContext.AbortActiveAction();
            _isExecuting.Dispose();
        }
    }
}
