using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class SkillSystem : ISkillSystem, IDisposable
{
    public Observable<bool> IsSkillUsable => _isSkillUsable;
    private readonly SkillContext _skillContext;
    private readonly Dictionary<SkillDefinition, float> _nestUsableTimes = new Dictionary<SkillDefinition, float>();
    private readonly ReactiveProperty<bool> _isSkillUsable = new ReactiveProperty<bool>(true);
    private CancellationTokenSource _cts;
    private bool _disposed;

    public SkillSystem(SkillContext skillContext)
    {
        _skillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    public bool TryUseSkill(SkillDefinition definition)
    {
        //未定義、実行中、破棄済みの場合は使用不可。
        if (_disposed || _isSkillUsable.Value || definition == null || definition.Actions.Length == 0)
        {
            return false;
        }
        //クールダウン中なら使用不可。
        if (_nestUsableTimes.TryGetValue(definition, out float nextUsableTime) && Time.time < nextUsableTime)
        {
            return false;
        }

        _isSkillUsable.Value = true;
        _nestUsableTimes[definition] = Time.time + definition.Cooldown;
        CancellationTokenSource cst =new CancellationTokenSource();
        _cts = cst;
        ExecutAsync(definition, cst).Forget();
        return true;
    }

    /// <summary>
    /// クールダウンの残り時間を取得します。
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    public float GetCooldownRemaing(SkillDefinition definition)
    {
        if (definition == null || !_nestUsableTimes.TryGetValue(definition, out float nextUsableTime))
        {
            return 0f;
        }
        return Mathf.Max(0f, nextUsableTime - Time.time);
    }

    /// <summary>
    /// スキルの実行を非同期で行います。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    private async UniTask ExecutAsync(SkillDefinition skill, CancellationTokenSource cts)
    {
        try
        {
            foreach(var action in skill.Actions)
            {
                cts.Token.ThrowIfCancellationRequested();
                if(action == null)
                {
                    throw new InvalidOperationException($"スキルが設定されていない   {skill.SkillName}");
                }
                await action.ExecuteAsync(_skillContext, cts.Token);
            }
        }
        //プレイヤーが死んだ時とか
        catch (OperationCanceledException)
        {
            Debug.Log($"スキルの実行がキャンセルされました。{skill.SkillName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"スキルの実行中にエラーが発生しました。{skill.SkillName} {ex}");
        }
        finally
        {
            _skillContext.AbortActiveAction();
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
            if (!_disposed) _isSkillUsable.Value = false;
        }
    }

    /// <summary>
    /// スキルの実行をキャンセルします。
    /// </summary>
    public void CancelActiveSkill()
    {
        if(!_disposed && _cts != null)
        {
            _cts.Cancel();
        }
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _isSkillUsable.Dispose();
    }
}
