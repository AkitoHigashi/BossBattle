using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// 攻撃ノードの共通処理
/// 攻撃までの時間だけ予兆を描画 → 攻撃 → Recovery Time の間待ってSuccess
/// 攻撃までの時間は、基本は Warning Area の Warning Time、上書きONなら Time To Attack
/// </summary>
public abstract class AttackNode : ActionNode
{
    [FormerlySerializedAs("_damageMultiplier")]
    [SerializeField, Min(0f), Tooltip("キャラの基本攻撃力に掛ける、この攻撃固有の倍率。")]
    private float _attackMultiplier = 1.5f;

    [SerializeField, Tooltip("ONにすると、Warning Area の Warning Time ではなく Time To Attack を使う。アニメーションに合わせたい時に使う")]
    private bool _overrideTimeToAttack;

    [FormerlySerializedAs("_predictionDuration")]
    [SerializeField, Min(0f), Tooltip("開始から攻撃までの秒数。上書きONの時か、Warning Area が未設定の時に使う")]
    private float _timeToAttack;

    [FormerlySerializedAs("_range")]
    [SerializeField, Tooltip("予兆の範囲。そのまま当たり判定になる。未設定なら予兆を出さず、即時攻撃になる")]
    private SectorRangeSettings _warningArea;

    [SerializeField, Tooltip("敵の正面を0度とする攻撃範囲の水平回転。90度で右、180度で後ろ、270度で左。")]
    private float _yawOffset;

    [FormerlySerializedAs("_sampleRadius")]
    [SerializeField, Min(0.01f), Tooltip("足元の地面を探す半径")]
    private float _groundSearchRadius = 2f;

    [FormerlySerializedAs("_attackInterval"), FormerlySerializedAs("_attackDuration")]
    [SerializeField, Min(0f), Tooltip("攻撃後の硬直。この間Runningを返す")]
    private float _recoveryTime;

    private enum Phase
    {
        WindUp,
        Attack,
        Recovery
    }

    private Phase _phase;
    private float _startTime;
    private float _recoveryEndTime;
    private bool _canAttack;
    private bool _isWarningShown;
    private AttackPose _pose;

    /// <summary>整数への丸め前のダメージ。敵ごとの基本攻撃力と、攻撃ごとの倍率を組み合わせる。</summary>
    protected float Damage => _blackBoard.AttackDamage * Mathf.Max(0f, _attackMultiplier);

    /// <summary>予兆を出した位置。エフェクトの生成位置などに使う</summary>
    protected AttackPose Pose => _pose;

    protected float RecoveryTime => Mathf.Max(0f, _recoveryTime);

    /// <summary>開始から攻撃までの秒数。範囲があれば範囲側の時間、上書きONか範囲なしならノード側の時間</summary>
    private float TimeToAttack =>
        _warningArea != null && !_overrideTimeToAttack ? _warningArea.WarningTime : Mathf.Max(0f, _timeToAttack);

    protected override void OnStart()
    {
        _phase = Phase.WindUp;
        _startTime = Time.time;
        _isWarningShown = false;

        // 範囲ありで地面が見つからなければ攻撃できない
        _canAttack = TryDecidePose();
        if (!_canAttack || _warningArea == null)
            return;

        // PredictionView が無ければ描画だけ飛ばす。待ち時間と当たり判定は変わらない
        var view = _blackBoard.PredictionView;
        if (view != null && view.isActiveAndEnabled)
            _isWarningShown = view.Show(_warningArea.Range, _pose, this);
    }

    protected override NodeStatus OnUpdate()
    {
        if (!_canAttack)
            return NodeStatus.Failure;

        if (_phase == Phase.WindUp)
        {
            float timeToAttack = TimeToAttack;
            float progress = timeToAttack <= 0f ? 1f :
                Mathf.Clamp01((Time.time - _startTime) / timeToAttack);
            if (_isWarningShown)
                _blackBoard.PredictionView.SetProgress(progress, this);
            if (progress < 1f)
                return NodeStatus.Running;

            _phase = Phase.Attack;
        }

        if (_phase == Phase.Attack)
        {
            var status = OnAttack();
            if (status != NodeStatus.Success)
                return status;

            // Hit()を呼ばずに終わった攻撃
            if (_phase == Phase.Attack)
                BeginRecovery();
        }

        return Time.time < _recoveryEndTime ? NodeStatus.Running : NodeStatus.Success;
    }

    /// <summary>
    /// 攻撃の本体。攻撃する瞬間に Hit() を呼んでSuccessを返す。続けるならRunning
    /// </summary>
    protected abstract NodeStatus OnAttack();

    /// <summary>
    /// 範囲内（範囲未設定なら無条件）のターゲットにダメージを与える
    /// </summary>
    protected void Hit()
    {
        // TakeDamageの通知から再評価されても二重に当たらないよう、先に硬直へ進める
        BeginRecovery();

        var target = _blackBoard.Target;
        if (target == null)
            return;
        if (_warningArea != null &&
            !_warningArea.Contains(_pose.Origin, _pose.Forward, target.transform.position))
            return;
        if (!target.TryGetComponent(out IDamageable damageable))
        {
            Debug.LogWarning("攻撃対象にIDamageableがありません。", target);
            return;
        }

        // プレイヤーの攻撃と同じAttackData経由で渡す。敵の攻撃にブレイク値はない
        int damage = Mathf.RoundToInt(Damage);
        new AttackData(damage, 0f).ApplyTo(damageable);
        Debug.Log($"{name}：{damage}ダメージ", target);
    }

    protected override void OnEnd() => HideWarning();

    private void BeginRecovery()
    {
        _phase = Phase.Recovery;
        _recoveryEndTime = Time.time + RecoveryTime;

        // 攻撃した瞬間に予兆を消す
        HideWarning();
    }

    private void HideWarning()
    {
        if (_isWarningShown && _blackBoard.PredictionView != null)
            _blackBoard.PredictionView.Hide(this);
        _isWarningShown = false;
    }

    /// <summary>
    /// 範囲ありなら足元の地面で位置を確定する。範囲なしなら今の位置
    /// </summary>
    private bool TryDecidePose()
    {
        var owner = _blackBoard?.Transform;
        if (owner == null)
            return false;

        if (_warningArea == null)
        {
            _pose = new AttackPose(owner.position, owner.forward);
            return true;
        }

        var agent = _blackBoard.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 origin = owner.position;
        Vector3 ground = origin - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, _groundSearchRadius), filter))
            return false;

        // 位置・向きを一度だけ確定し、表示と命中判定で同じ値を使う
        origin.y = hit.position.y;
        Vector3 forward = SectorRange.GetHorizontalForward(owner.forward);
        forward = Quaternion.AngleAxis(_yawOffset, Vector3.up) * forward;
        _pose = new AttackPose(origin, forward);
        return true;
    }
}
