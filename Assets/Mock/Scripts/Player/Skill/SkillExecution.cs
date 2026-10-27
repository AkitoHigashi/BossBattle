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
    public UniTask Completion => _completionSource.Task;
    public SkillDefinition Definition { get; }
    public Vector3 Direction { get; }
    public bool IsHeld { get; private set; }
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
    }

    /// <summary>
    /// 入力の通知と保持。
    /// </summary>
    /// <param name="phase"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
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
                if (!IsHeld)
                    return;

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
