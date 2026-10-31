using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

/// <summary>
/// 溜めた結果、後続の攻撃の威力をどう変えるか。
/// </summary>
public enum ChargePowerMode
{
    /// <summary>威力を変えない。</summary>
    None,
    /// <summary>溜めた時間に比例して威力が上がる（スキル5：溜め3連）。</summary>
    ScaleByChargeTime,
    /// <summary>指定ゾーン内で離したときだけ高出力になる（スキル6：溜め斬りつけ）。</summary>
    JustTiming
}

/// <summary>
/// 長押しで「溜め」を行い、離したら正常終了する Action（スキル5・6で共用）。
/// 攻撃そのものは行わず、SkillDefinition の Actions でこの後ろに並べた攻撃 Action が実行される。
/// </summary>
/// <remarks>
/// 溜めと攻撃を別 Action に分けたのは、スキル5（3連撃）とスキル6（単発）で攻撃部分だけが異なるため。
/// 溜め中に中断された場合は OperationCanceledException が SkillSystem まで伝わり、
/// 後続の攻撃 Action は実行されない（溜め途中のキャンセルで攻撃が出ることはない）。
///
/// 溜め結果の威力は SkillExecution.DamageMultiplier に書き込み、後続の PlayerAttackAction が読む。
/// ScriptableObject は全プレイヤーで共有されるため、実行ごとの値をここに保存してはいけない。
///
/// 発動タイミング：
/// ・最低溜め時間以降に離した → 離した時点で発動する
/// ・最低溜め時間より前に離した → 最低溜め時間まで溜めてから発動する
/// ・押し続けた → 最大溜め時間で自動的に発動する（押しっぱなしで終わらなくなるのを防ぐ安全上限）
/// </remarks>
[CreateAssetMenu(fileName = "ChargeAction", menuName = "Skills/ChargeAction")]
public class ChargeAction : SkillAction
{
    [Tooltip("これより早く離しても、この秒数までは溜めを続ける")]
    [SerializeField, Min(0f)] private float _minChargeTime = 0f;
    [Tooltip("押し続けても、この秒数で溜めを終えて発動する")]
    [SerializeField, Min(0f)] private float _maxChargeTime = 10f;
    [Tooltip("溜め中に true にする Animator の Bool パラメータ名")]
    [SerializeField] private string _chargingBoolName = "Charging";
    [Tooltip("溜め中はその場に留まるか（通常移動を止める）")]
    [SerializeField] private bool _lockMovement = true;

    [Header("威力")]
    [SerializeField] private ChargePowerMode _powerMode = ChargePowerMode.None;

    [Tooltip("溜め結果の倍率をブレイク値にも掛けるか。OFFならダメージだけが強化される")]
    [SerializeField] private bool _multiplyBreakValue = false;

    [Header("ScaleByChargeTime：溜め時間で威力上昇")]
    [Tooltip("この秒数溜めると最大倍率になる。それ以上溜めても倍率は上がらない")]
    [SerializeField, Min(0.01f)] private float _fullChargeTime = 2f;
    [Tooltip("最大まで溜めたときのダメージ倍率。溜め0秒は1倍で、その間は直線的に上がる")]
    [SerializeField, Min(1f)] private float _maxChargeMultiplier = 2f;

    [Header("JustTiming：ゾーン内で離すと高出力")]
    [Tooltip("ジャストゾーンの開始（溜め開始からの秒数）")]
    [SerializeField, Min(0f)] private float _justWindowStart = 0.8f;
    [Tooltip("ジャストゾーンの終了（溜め開始からの秒数）")]
    [SerializeField, Min(0f)] private float _justWindowEnd = 1.0f;
    [Tooltip("ジャスト成功時のダメージ倍率。外した場合は1倍（通常出力）")]
    [SerializeField, Min(1f)] private float _justMultiplier = 2f;
    [Tooltip("ゲージ1本分の秒数。ジャストゾーンはこの長さに対する位置として表示される")]
    [SerializeField, Min(0.01f)] private float _justGaugeDuration = 1.5f;

    public override async UniTask ExecuteAsync(SkillContext context, SkillExecution execution)
    {
        execution.Token.ThrowIfCancellationRequested();

        Animator animator = context.Animator;
        CharacterMotor motor = context.CharacterMotor;
        ChargeGauge gauge = context.ChargeGauge;
        int chargingHash = Animator.StringToHash(_chargingBoolName);

        // 設定ミスは溜めを始める前に検出し、溜めモーションが出ないまま待ち続けるのを防ぐ。
        if (!HasBoolParameter(animator, _chargingBoolName))
            throw new InvalidOperationException($"Animator Boolが存在しません: {_chargingBoolName}");

        float startedAt = Time.time;
        float chargedTime;
        bool releasedByPlayer;
        try
        {
            if (_lockMovement)
                motor.SetOverrideVelocity(Vector3.zero);
            animator.SetBool(chargingHash, true);
            ShowGauge(gauge);

            while (true)
            {
                execution.Token.ThrowIfCancellationRequested();

                float elapsed = Time.time - startedAt;
                if (_powerMode != ChargePowerMode.None)
                    gauge.SetProgress(elapsed / GetGaugeDuration());

                if (elapsed >= _maxChargeTime)
                {
                    chargedTime = elapsed;
                    releasedByPlayer = false;
                    break;
                }
                if (elapsed >= _minChargeTime && !execution.IsHeld)
                {
                    chargedTime = elapsed;
                    releasedByPlayer = true;
                    break;
                }

                await UniTask.NextFrame(execution.Token);
            }
        }
        finally
        {
            // 溜め完了・中断・例外のいずれでも、移動停止と溜めモーションを解除する。
            // 解除しないと、回避で中断した後も動けない・溜めポーズのまま、という状態が残る。
            if (_lockMovement)
                motor.ClearOverrideVelocity();
            // 離した瞬間・中断した瞬間にゲージを消す。攻撃中まで残すと「まだ溜めている」と誤解させる。
            gauge.Hide();
            // GameObject ごと無効化された場合、Animator のパラメータは再有効化時に既定値へ戻るため触らない。
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetBool(chargingHash, false);
        }

        // ここに来るのは溜めが正常に終わったときだけ。中断時は倍率を設定しない。
        float multiplier = EvaluateMultiplier(chargedTime, releasedByPlayer);
        execution.SetDamageMultiplier(multiplier);
        if (_multiplyBreakValue)
            execution.SetBreakMultiplier(multiplier);

        // 調整時に数値で確認できるよう、溜め結果をログに出す。
        Debug.Log($"[ChargeAction] {execution.Definition.SkillName}: 溜め {chargedTime:0.00}s → 倍率 x{multiplier:0.00}");
    }

    /// <summary>
    /// 威力モードに合わせてゲージを表示する。威力が変わらない溜めではゲージを出さない。
    /// </summary>
    private void ShowGauge(ChargeGauge gauge)
    {
        switch (_powerMode)
        {
            case ChargePowerMode.ScaleByChargeTime:
                gauge.Show(hasJustZone: false);
                break;

            case ChargePowerMode.JustTiming:
                // 秒数をゲージ上の位置（0〜1）に変換して渡す。View は秒数を知らなくてよい。
                gauge.Show(
                    hasJustZone: true,
                    zoneStart: _justWindowStart / _justGaugeDuration,
                    zoneEnd: _justWindowEnd / _justGaugeDuration);
                break;
        }
    }

    /// <summary>
    /// ゲージが満タンになるまでの秒数。
    /// 溜め時間で威力が上がる場合は「最大倍率に達する時間」で満タンにし、それ以上溜めても伸びないことを見せる。
    /// </summary>
    private float GetGaugeDuration()
    {
        return _powerMode == ChargePowerMode.ScaleByChargeTime ? _fullChargeTime : _justGaugeDuration;
    }

    private float EvaluateMultiplier(float chargedTime, bool releasedByPlayer)
    {
        switch (_powerMode)
        {
            case ChargePowerMode.ScaleByChargeTime:
                // 0秒で1倍、_fullChargeTime で最大倍率。段階制にする場合はここを差し替える。
                float ratio = Mathf.Clamp01(chargedTime / _fullChargeTime);
                return Mathf.Lerp(1f, _maxChargeMultiplier, ratio);

            case ChargePowerMode.JustTiming:
                // 自動発動（最大溜め時間到達）はプレイヤーが離したわけではないので、ジャスト扱いにしない。
                bool isJust = releasedByPlayer
                    && chargedTime >= _justWindowStart
                    && chargedTime <= _justWindowEnd;
                return isJust ? _justMultiplier : 1f;

            default:
                return 1f;
        }
    }

    private static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (var parameter in animator.parameters)
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
                return true;

        return false;
    }

    private void OnValidate()
    {
        // 最大が最低より短いと「離すまで待つ」区間がなくなり、意図が分かりにくくなるため揃える。
        _maxChargeTime = Mathf.Max(_maxChargeTime, _minChargeTime);
        // ゾーンの終了が開始より前だと、ジャストが絶対に成立しなくなるため揃える。
        _justWindowEnd = Mathf.Max(_justWindowEnd, _justWindowStart);
        // ゾーンがゲージからはみ出すと表示できないため、ゲージはゾーンの終わりより長くする。
        _justGaugeDuration = Mathf.Max(_justGaugeDuration, _justWindowEnd);
    }
}
