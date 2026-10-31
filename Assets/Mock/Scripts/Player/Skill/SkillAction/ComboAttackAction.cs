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
                // 発動時点の向きを取得
                Vector3 forward = motor.transform.forward;
                forward.y = 0f;

                // 設定された方向モードで移動方向を決定
                Vector3 moveDirection = _direction switch
                {
                    MoveDirectionMode.Forward => forward,
                    MoveDirectionMode.AimDirection => execution.CurrentAimDirection,
                    MoveDirectionMode.Back => -forward,
                    MoveDirectionMode.InputDirection => execution.GetPressDirection(stage),
                    _ => Vector3.zero
                };

                moveDirection.y = 0f;
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

            // PressCount は連撃全体で累積する。入力済みなら待たずに次の段へ進む。
            // 例：1段目の間に合計3回押すと、2・3段目の両方が予約される。
            while (execution.PressCount < requiredPressCount)
            {
                execution.Token.ThrowIfCancellationRequested();

                if (Time.time - waitStartedAt >= _followUpWindow)
                    return;

                await UniTask.NextFrame(execution.Token);
            }
        }
    }
}
