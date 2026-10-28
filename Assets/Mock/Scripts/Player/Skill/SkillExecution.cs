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
    public Vector3 Direction { get; }
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
                // 押しっぱなしで同じ通知が来ても、離して再度押すまでは回数を増やさない。
                if (IsHeld)
                    return;

                IsHeld = true;
                PressCount++;
                break;

            case SkillInputPhase.Released:
                // 入力状態を更新するだけで、スキルの実行自体はキャンセルしない。
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
