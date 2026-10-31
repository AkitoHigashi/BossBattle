using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public enum SkillInputPhase
{
    Pressed,
    Released
}

public class SkillExecution : IDisposable
{
    public CancellationToken Token => _cts.Token;
    // Cancel の呼び出し時ではなく、SkillSystem の終了処理で NotifyCompleted が呼ばれると完了する。
    public UniTask Completion => _completionSource.Task;
    public SkillDefinition Definition { get; }
    /// <summary>
    /// スキル発動時の方向
    /// </summary>
    public Vector3 Direction { get; }
    /// <summary>
    /// 現在のマウス方向
    /// </summary>
    public Vector3 CurrentAimDirection { get; private set; }
    // この1回の実行中に受け取った入力状態。実際の溜め・派生の判断は各 Action 側で行う。
    public bool IsHeld { get; private set; }
    // 最初の発動入力を含む押下回数。新しい SkillExecution では 0 から数え直す。
    public int PressCount { get; private set; }

    private readonly CancellationTokenSource _cts = new();
    private readonly UniTaskCompletionSource _completionSource = new();
    private bool _disposed;

    public SkillExecution(SkillDefinition definition, Vector3 direction)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        Definition = definition;
        Direction = direction;
        CurrentAimDirection = direction.normalized;
    }

    public void SetAimDirection(Vector3 direction)
    {
        if (_disposed)
            return;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        CurrentAimDirection = direction.normalized;
    }

    public void ReceiveInput(SkillInputPhase phase)
    {
        if (_disposed || _cts.IsCancellationRequested)
            return;

        switch (phase)
        {
            case SkillInputPhase.Pressed:
                if (IsHeld)
                    return;

                IsHeld = true;
                PressCount++;
                break;

            case SkillInputPhase.Released:
                IsHeld = false;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }

    public void Cancel()
    {
        if (_disposed)
            return;
        // 中断を要求するだけで、ここでは後片付け完了を通知しない。
        _cts.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _cts.Dispose();
        _disposed = true;
    }

    public void NotifyCompleted()
    {
        _completionSource.TrySetResult();
    }
}
