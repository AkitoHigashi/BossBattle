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

    public SkillSystem(SkillContext skillContext)
    {
        _skillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary>
    /// スキルの実行を試す。
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    public bool TryUseSkill(SkillDefinition definition, Vector3 direction)
    {
        //未定義、実行中、破棄済みの場合は使用不可。
        if (_disposed || _isExecuting.Value || definition == null || definition.Actions == null || definition.Actions.Length == 0)
        {
            return false;
        }
        //クールダウン中なら使用不可。
        if (_nextUsableTimes.TryGetValue(definition, out float nextUsableTime) && Time.time < nextUsableTime)
        {
            return false;
        }

        var execution = new SkillExecution(definition, direction);
        execution.ReceiveInput(SkillInputPhase.Pressed);
        _currentExecution = execution;
        if (definition.CooldownPolicy == CooldownStartPolicy.OnStart)
            _nextUsableTimes[definition] = Time.time + definition.Cooldown;
        _isExecuting.Value = true;
        ExecutAsync(execution).Forget();
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
        if (_disposed || definition == null)
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
                if (execution.Definition.CooldownPolicy == CooldownStartPolicy.OnEnd)
                    _nextUsableTimes[execution.Definition] = Time.time + execution.Definition.Cooldown;
                execution.Dispose();
                if (ReferenceEquals(_currentExecution, execution))
                {
                    _currentExecution = null;

                    if (!_disposed)
                        _isExecuting.Value = false;
                }
                execution.NotifyCompleted();
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
