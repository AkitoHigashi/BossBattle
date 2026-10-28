using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ComboAttackAction",menuName = "Skills/ComboAttackAction")]
public class ComboAttackAction : SkillAction
{
    [SerializeField] private PlayerAttackAction[] _attacks;
    [Tooltip("次の連撃を発動するための追加入力を待つ時間")]
    [SerializeField, Min(0f)] private float _followUpWindow = 1f;

    public override async UniTask ExecuteAsync(SkillContext context,SkillExecution execution)
    {
        if(_attacks==null || _attacks.Length==0||_attacks.Length>3)
            throw new InvalidOperationException("連撃は1〜3段に設定してください。");

        for (int stage = 0; stage < _attacks.Length; stage++)
        {
            execution.Token.ThrowIfCancellationRequested();
            PlayerAttackAction attack = _attacks[stage];

            if (attack == null)
                throw new InvalidOperationException($"連撃{stage + 1}段目が未設定です。");

            // 各段の攻撃アニメーション・判定を実行
            await attack.ExecuteAsync(context, execution);

            // 最終段なら終了
            if (stage == _attacks.Length - 1)
                break;

            // 初回入力を1回目として数えるため、
            // 次の段には stage + 2 回の入力が必要
            int requiredPressCount = stage + 2;
            float waitStartedAt = Time.time;

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
