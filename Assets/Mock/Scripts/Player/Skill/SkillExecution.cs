using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// スキルボタンの入力段階。InputAction の performed / canceled に対応する。
/// </summary>
public enum SkillInputPhase
{
    /// <summary>押した。</summary>
    Pressed,
    /// <summary>離した。</summary>
    Released
}

/// <summary>
/// スキル1回分の「実行中の状態」をまとめたもの。発動のたびに SkillSystem が新しく作り、終了時に破棄する。
/// </summary>
/// <remarks>
/// SkillDefinition / SkillAction は ScriptableObject なので全プレイヤーで共有される。
/// 押下状態・照準・倍率のように実行ごとに変わる値をそちらへ書くと、別の実行と混ざってしまう。
/// そのため実行ごとの値はすべてこのクラスに持たせ、Action には引数で渡している。
/// </remarks>
public class SkillExecution : IDisposable
{
    // Action はこの Token を await に渡し、中断（回避の割り込み・無効化）を検知する。
    public CancellationToken Token => _cts.Token;
    // Cancel の呼び出し時ではなく、SkillSystem の終了処理で NotifyCompleted が呼ばれると完了する。
    public UniTask Completion => _completionSource.Task;
    // 実行しているスキルの設定。どのスキルへの入力かを SkillSystem が判定するのにも使う。
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
    // 溜め結果などで決まる、この実行中の攻撃のダメージ倍率。
    // 実行ごとに変わる値なので、共有される ScriptableObject ではなくここに持つ。
    public float DamageMultiplier { get; private set; } = 1f;
    // 同じくブレイク値の倍率。ダメージだけ強化するスキルもあるため、ダメージ倍率とは別に持つ。
    public float BreakMultiplier { get; private set; } = 1f;

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

    /// <summary>
    /// 実行中の照準方向を更新する。PlayerController が毎フレーム呼ぶ。
    /// 水平成分だけを使い、マウスがキャラクターの真上にあるなど方向が定まらないときは前回の値を保つ。
    /// </summary>
    public void SetAimDirection(Vector3 direction)
    {
        if (_disposed)
            return;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        CurrentAimDirection = direction.normalized;
    }

    /// <summary>
    /// 実行中のスキルへのボタン入力を記録する。
    /// ここでは状態を更新するだけで、連撃・長押し・溜めとしてどう扱うかは各 Action が決める。
    /// </summary>
    public void ReceiveInput(SkillInputPhase phase)
    {
        // 中断を要求された実行は後片付け中なので、入力を受け付けても意味がない。
        if (_disposed || _cts.IsCancellationRequested)
            return;

        switch (phase)
        {
            case SkillInputPhase.Pressed:
                // 押しっぱなしのまま Pressed が重ねて届いても、押下回数を二重に数えない。
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

    /// <summary>
    /// この実行で以降に出す攻撃のダメージ倍率を設定する。負の値は 0 に切り上げる。
    /// </summary>
    public void SetDamageMultiplier(float multiplier)
    {
        if (_disposed)
            return;

        DamageMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// この実行で以降に出す攻撃のブレイク値倍率を設定する。負の値は 0 に切り上げる。
    /// </summary>
    public void SetBreakMultiplier(float multiplier)
    {
        if (_disposed)
            return;

        BreakMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// 基本の攻撃内容に、この実行の倍率（溜め結果など）を掛けたものを返す。
    /// 剣の攻撃と飛び道具で同じ計算を使うためにここへまとめている。
    /// </summary>
    public AttackData ApplyMultipliers(AttackData baseData)
    {
        return baseData.Scale(DamageMultiplier, BreakMultiplier);
    }

    /// <summary>
    /// この実行の中断を要求する。回避の割り込み・キャラクターの無効化・長押しの解放などから呼ばれる。
    /// </summary>
    public void Cancel()
    {
        if (_disposed)
            return;
        // 中断を要求するだけで、ここでは後片付け完了を通知しない。
        _cts.Cancel();
    }

    /// <summary>
    /// CancellationTokenSource を解放する。SkillSystem が実行の後片付けの最後に呼ぶ。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _cts.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// 後片付けまで終わったことを通知し、Completion を待っている割り込み側を進める。
    /// </summary>
    public void NotifyCompleted()
    {
        _completionSource.TrySetResult();
    }
}
