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
    [Tooltip("次の段の入力を待っている間は歩けるようにする（スキル中に歩けない設定でも）")]
    [SerializeField] private bool _canMoveWhileWaiting = true;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        if (_attacks == null || _attacks.Length == 0 || _attacks.Length > 3)
            throw new InvalidOperationException("連撃は1〜3段に設定してください。");

        CharacterMotor motor = context.CharacterMotor;
        // 回避で中断した連撃を再開するときは、途中の段から始める。
        int startStage = Mathf.Min(execution.StartStage, _attacks.Length - 1);
        if (startStage > 0)
            Log($"{startStage + 1}段目から再開");

        for (int stage = startStage; stage < _attacks.Length; stage++)
        {
            execution.Token.ThrowIfCancellationRequested();
            PlayerAttackAction attack = _attacks[stage];

            if (attack == null)
                throw new InvalidOperationException($"連撃{stage + 1}段目が未設定です。");

            // 振り終わった（判定が閉じた）時点から、回避で中断されても次の段から再開できるようにする。
            // 攻撃モーションの戻りの最中に回避したときも、「この段は出し終わった」と扱うため。
            // 最終段の後は続きがないので登録しない。
            AttackAnimationEvents events = context.AnimationEvents;
            int nextStage = stage + 1;
            bool hasNextStage = nextStage < _attacks.Length;
            void OnHitEnded()
            {
                if (execution.ResumeStage < 0)
                    execution.SetResumePoint(nextStage, Time.time + _followUpWindow, _followUpWindow);
            }
            if (hasNextStage && events != null)
                events.HitEnded += OnHitEnded;

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
                if (hasNextStage && events != null)
                    events.HitEnded -= OnHitEnded;
            }

            // 最終段なら終了
            if (stage == _attacks.Length - 1)
                break;

            // 初回入力（再開時は再開の入力）を1回目として数えるため、
            // この実行で進んだ段数 + 2 回の入力が必要
            int requiredPressCount = stage - startStage + 2;
            // 猶予の計測は各段の終了後から開始する（攻撃中の再生時間は含めない）。
            float waitStartedAt = Time.time;

            // 入力待ちの間に回避で中断されても、猶予内に押せば次の段から再開できるようにする。
            execution.SetResumePoint(stage + 1, waitStartedAt + _followUpWindow, _followUpWindow);
            if (_canMoveWhileWaiting)
                motor.SetLocomotionLocked(false);

            Log($"{stage + 1}段目終了。次の段には合計{requiredPressCount}回の押下が必要（現在{execution.PressCount}回）");

            // 押し直せる残り時間を記録し、下のスキル枠に表示させる（いつまでに押せばつながるかを見せるため）。
            try
            {
                // PressCount は連撃全体で累積する。入力済みなら待たずに次の段へ進む。
                // 例：1段目の間に合計3回押すと、2・3段目の両方が予約される。
                while (execution.PressCount < requiredPressCount)
                {
                    execution.Token.ThrowIfCancellationRequested();

                    float elapsed = Time.time - waitStartedAt;
                    if (elapsed >= _followUpWindow)
                    {
                        Log($"猶予{_followUpWindow}秒以内に押されなかったため、{stage + 1}段で終了");
                        execution.ClearResumePoint();
                        return;
                    }

                    execution.SetInputWindow(_followUpWindow - elapsed, _followUpWindow);
                    await UniTask.NextFrame(execution.Token);
                }
            }
            finally
            {
                // 次の段へ進む・猶予切れ・中断のどの場合も、受付時間の表示を消す。
                execution.ClearInputWindow();
            }

            // 次の段を振り終わる前に中断されたら、もう再開はできない（通常の中断としてクールダウンへ）。
            // 次の段を振り終わると、上の OnHitEnded で再び再開できるようになる。
            execution.ClearResumePoint();
            // 攻撃中は、スキルの設定どおり歩けない状態に戻す。
            motor.SetLocomotionLocked(!execution.Definition.CanMoveDuringSkill);
            Log($"{stage + 2}段目へ");
        }
    }

    private void Log(string message)
    {
        if (_logStages)
            Debug.Log($"[ComboAttackAction] {message}");
    }
}
