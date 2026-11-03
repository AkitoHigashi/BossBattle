using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 長押ししている間、同じ攻撃を繰り返す Action（スキル4：前方連続突き）。
/// ボタンを離した瞬間に、出している途中の突きも含めて中断する。
/// </summary>
/// <remarks>
/// ComboAttackAction は「追加の押下回数」で次段へ進むため、押しっぱなしで繰り返す本スキルとは
/// 入力の解釈が異なる。条件分岐を足すと連撃側の読みやすさが落ちるため、別クラスにしている。
/// 実行中の状態（何発目か・経過時間）はローカル変数で持ち、ScriptableObject には保存しない。
/// </remarks>
[CreateAssetMenu(fileName = "HoldRepeatAttackAction", menuName = "Skills/HoldRepeatAttackAction")]
public class HoldRepeatAttackAction : SkillAction
{
    // PlayerAttackAction に限定せず SkillAction で受けるのは、波動付きの突き（ProjectileAttackAction）も繰り返せるようにするため。
    [Tooltip("1回分の突き。PlayerAttackAction（突きのみ）か ProjectileAttackAction（突き＋波動）を設定する")]
    [SerializeField] private SkillAction _attack;
    [Tooltip("長押しを続けても、この回数で終了する（1発目を含む）。0なら回数制限なし（継続時間だけで終了）")]
    [SerializeField, Min(0)] private int _maxAttackCount = 0;
    [Tooltip("1発目の開始からこの秒数を過ぎたら、次の突きを出さずに終了する")]
    [SerializeField, Min(0f)] private float _maxHoldDuration = 4f;
    [Tooltip("突きと突きの間に空ける秒数。0なら前の突きが終わり次第すぐ次を出す")]
    [SerializeField, Min(0f)] private float _interval = 0f;
    [Tooltip("各突きで踏み込む方向")]
    [SerializeField] private MoveDirectionMode _direction = MoveDirectionMode.AimDirection;
    [Tooltip("各突きの開始時に踏み込む速度。0なら移動しない")]
    [SerializeField, Min(0f)] private float _stepSpeed = 2f;
    [Tooltip("各突きの踏み込みを続ける秒数")]
    [SerializeField, Min(0f)] private float _stepDuration = 0.15f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        if (_attack == null)
            throw new InvalidOperationException("HoldRepeatAttackAction の攻撃が未設定です。");

        CharacterMotor motor = context.CharacterMotor;
        ChargeGauge gauge = context.ChargeGauge;
        float startedAt = Time.time;

        // 「離したら即キャンセル」とゲージ表示のため、突きの再生中も毎フレーム監視する。
        // PlayerAttackAction は execution.Token でしか止められないので、解放を検知したら実行ごと中断する。
        // 中断後の後片付け（攻撃判定・移動・中断用Animator遷移）は既存の finally と SkillSystem が行う。
        using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(execution.Token);
        // 残りの継続時間を表すため、満タンから始めて減らしていく。
        gauge.Show(hasJustZone: false);
        gauge.SetProgress(1f);
        MonitorAsync(execution, gauge, startedAt, watchCts.Token).Forget();

        try
        {
            // 仕様上の上限は継続時間（4秒）なので、回数上限は 0 で無効化できるようにしている。
            for (int count = 0; _maxAttackCount == 0 || count < _maxAttackCount; count++)
            {
                execution.Token.ThrowIfCancellationRequested();

                // 継続時間を過ぎたら、次の突きを出さずに正常終了する。
                // 再生中の突きは途中で切らず、最後まで出してから終わる。
                if (count > 0 && Time.time - startedAt >= _maxHoldDuration)
                    return;

                if (count > 0 && _interval > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(_interval), cancellationToken: execution.Token);

                try
                {
                    Vector3 stepDirection = MoveDirectionResolver.Resolve(_direction, motor, execution);
                    if (_stepSpeed > 0f && _stepDuration > 0f && stepDirection.sqrMagnitude > 0.0001f)
                        motor.SetOverrideVelocity(stepDirection.normalized * _stepSpeed, _stepDuration);

                    // 攻撃 State を抜けるまで待つため、次の突きが前の突きに重なることはない。
                    await _attack.ExecuteAsync(context, execution);
                }
                finally
                {
                    // 突きが中断されても踏み込みが残らないよう、1発ごとに解除する。
                    motor.ClearOverrideVelocity();
                }
            }
        }
        finally
        {
            // 正常終了後に監視が残ると、後から Cancel を呼んでしまうため必ず止める。
            watchCts.Cancel();
            // 終了・中断のどちらでも、ゲージを残さない。
            gauge.Hide();
        }
    }

    /// <summary>
    /// 毎フレーム、残りの継続時間をゲージへ反映し、ボタンが離されたらこのスキルの実行を中断する。
    /// </summary>
    private async UniTaskVoid MonitorAsync(SkillExecution execution, ChargeGauge gauge, float startedAt, CancellationToken token)
    {
        // 継続時間が 0 の設定でも割り算で壊れないよう、下限を設ける。
        float duration = Mathf.Max(_maxHoldDuration, 0.01f);

        while (true)
        {
            gauge.SetProgress(1f - (Time.time - startedAt) / duration);

            if (!execution.IsHeld)
            {
                execution.Cancel();
                return;
            }

            // 監視の停止（正常終了・別要因の中断）は例外ではなく戻り値で受け取り、ログを汚さない。
            bool stopped = await UniTask.NextFrame(token).SuppressCancellationThrow();
            if (stopped)
                return;
        }
    }
}
