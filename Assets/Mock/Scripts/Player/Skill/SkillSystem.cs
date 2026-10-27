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
            // 同じスキルへの入力なら通知する
            if (_currentExecution.Definition == definition)
                _currentExecution.ReceiveInput(phase);
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
    /// 現在のスキルと次のスキルを比較し、割り込みを制御する
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public async UniTask<bool> TryUseSkillAsync(SkillDefinition definition, Vector3 direction, CancellationToken requestToken)
    {
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
                oldExecution.Cancel();
                await oldExecution.Completion;
                if (requestToken.IsCancellationRequested)
                    return false;
                if (!CanUseSkill(definition) || _currentExecution != null || _isExecuting.Value)
                    return false;

                StartSkill(definition, direction);
                return true;
            }
            finally
            {
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
        execution.ReceiveInput(SkillInputPhase.Pressed);
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
        //未定義、実行中、破棄済みの場合は使用不可。
        if (_disposed || definition == null || definition.Actions == null || definition.Actions.Length == 0)
        {
            return false;
        }
        //クールダウン中なら使用不可。
        if (_nextUsableTimes.TryGetValue(definition, out float nextUsableTime) && Time.time < nextUsableTime)
        {
            return false;
        }

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
            foreach (var action in execution.Definition.Actions)
            {
                execution.Token.ThrowIfCancellationRequested();
                if (action == null)
                {
                    throw new InvalidOperationException($"スキルが設定されていない   {execution.Definition.SkillName}");
                }
                await action.ExecuteAsync(_skillContext, execution);
            }
        }
        //プレイヤーが死んだ時とか
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
            try
            {
                _skillContext.AbortActiveAction();
            }
            finally
            {
                try
                {
                    if (execution.Definition.CooldownPolicy == CooldownStartPolicy.OnEnd)
                        _nextUsableTimes[execution.Definition] = Time.time + execution.Definition.Cooldown;
                    execution.Dispose();
                    if (ReferenceEquals(_currentExecution, execution))
                    {
                        _currentExecution = null;

                        if (!_disposed)
                            _isExecuting.Value = false;
                    }
                }
                finally
                {
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
            _currentExecution?.Cancel();
        }
        finally
        {
            _skillContext.AbortActiveAction();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
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
}
