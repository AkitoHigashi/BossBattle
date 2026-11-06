using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ComboAttackAction", menuName = "Skills/ComboAttackAction")]
public class ComboAttackAction : SkillAction
{
    [SerializeField] private PlayerAttackAction[] _attacks;
    // 各段の攻撃終了後に追加入力を待つ最大時間。攻撃中に受け取った入力も有効。
    [Tooltip("次の連撃を発動するための追加入力を待つ時間")]
    [SerializeField, Min(0f)] private float _followUpWindow = 1f;
    [SerializeField, Min(0f)] private float _forwardSpeed = 2f;
    [SerializeField, Min(0f)] private float _duration = 0.3f;
    [SerializeField] private MoveDirectionMode _direction;
    [Tooltip("各段の開始・入力待ち・猶予切れを Console に出す（連撃のつながりを確認するとき用）")]
    [SerializeField] private bool _logStages = false;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        if (_attacks == null || _attacks.Length == 0 || _attacks.Length > 3)
            throw new InvalidOperationException("連撃は1〜3段に設定してください。");

        CharacterMotor motor = context.CharacterMotor;

        for (int stage = 0; stage < _attacks.Length; stage++)
        {
            execution.Token.ThrowIfCancellationRequested();
            PlayerAttackAction attack = _attacks[stage];

            if (attack == null)
                throw new InvalidOperationException($"連撃{stage + 1}段目が未設定です。");

            try
            {
                // 設定された方向モードで、この段の移動方向を決定
                Vector3 moveDirection = MoveDirectionResolver.Resolve(_direction, motor, execution);
                //スキル発動時の前進。
                if(moveDirection.sqrMagnitude>0.001f)
                    motor.SetOverrideVelocity(moveDirection * _forwardSpeed, _duration);

                // 判定の終了だけでなく、攻撃ステートを退出するまで待って次の段へ進む。
                await attack.ExecuteAsync(context, execution);
            }
            finally
            {
                motor.ClearOverrideVelocity();
            }

            // 最終段なら終了
            if (stage == _attacks.Length - 1)
                break;

            // 初回入力を1回目として数えるため、
            // 次の段には stage + 2 回の入力が必要
            int requiredPressCount = stage + 2;
            // 猶予の計測は各段の終了後から開始する（攻撃中の再生時間は含めない）。
            float waitStartedAt = Time.time;

            Log($"{stage + 1}段目終了。次の段には合計{requiredPressCount}回の押下が必要（現在{execution.PressCount}回）");

            // 押し直せる残り時間を頭上ゲージで見せる。ゲージがあると「いつまでに押せばつながるか」が分かる。
            ChargeGauge gauge = context.ChargeGauge;
            try
            {
                if (execution.PressCount < requiredPressCount && _followUpWindow > 0f)
                    gauge.Show(hasJustZone: false);

                // PressCount は連撃全体で累積する。入力済みなら待たずに次の段へ進む。
                // 例：1段目の間に合計3回押すと、2・3段目の両方が予約される。
                while (execution.PressCount < requiredPressCount)
                {
                    execution.Token.ThrowIfCancellationRequested();

                    float elapsed = Time.time - waitStartedAt;
                    if (elapsed >= _followUpWindow)
                    {
                        Log($"猶予{_followUpWindow}秒以内に押されなかったため、{stage + 1}段で終了");
                        return;
                    }

                    gauge.SetProgress(1f - elapsed / _followUpWindow);
                    await UniTask.NextFrame(execution.Token);
                }
            }
            finally
            {
                // 次の段へ進む・猶予切れ・中断のどの場合も、待ち時間のゲージは消す。
                gauge.Hide();
            }

            Log($"{stage + 2}段目へ");
        }
    }

    private void Log(string message)
    {
        if (_logStages)
            Debug.Log($"[ComboAttackAction] {message}");
    }
}
