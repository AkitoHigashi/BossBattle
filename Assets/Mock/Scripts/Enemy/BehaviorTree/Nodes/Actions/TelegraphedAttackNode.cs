using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>地上の予兆・1回の発動判定・攻撃終了待ちを行うクラス</summary>
[CreateAssetMenu(fileName = "RangeAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/Range Attack Node")]
public sealed class TelegraphedAttackNode : AttackNode
{

    public override BtNode Clone(List<BtNode> clones)
    {
        var clone = (TelegraphedAttackNode)base.Clone(clones);
        clone._attack = null;
        clone._phase = Phase.Idle;
        clone._effectInstance = null;
        return clone;
    }

    //攻撃の状態
    private enum Phase
    {
        Idle,
        Prediction,
        Attack
    }

    [SerializeField] private SectorRangeSettings _range;
    [SerializeField, Min(0f)] private float _predictionDuration = 2f;
    [SerializeField, Min(0f), Tooltip("発動後何秒残すか")]
    private float _attackDuration = 1f;
    [SerializeField, Min(0.01f)] private float _sampleRadius = 2f;
    [SerializeField, Tooltip("発動時に生成し、攻撃終了時に破棄するエフェクト。未設定でも判定を実行する。")]
    private GameObject _effectPrefab;

    private AttackContext _attack;
    private Phase _phase;
    private float _predictionStartTime;
    private float _attackFinishTime;
    private GameObject _effectInstance;

    protected override void OnSuccess() => FinishAttack();
    protected override void OnFailure() => FinishAttack();
    protected override void OnAbort() => FinishAttack();

    protected override NodeStatus OnUpdate()
    {
        if (_phase == Phase.Idle && !StartPrediction())
            return NodeStatus.Failure;

        if (_phase == Phase.Prediction)
        {
            var view = _blackBoard.PredictionView;
            if (view == null || !view.isActiveAndEnabled || !view.IsVisibleFor(_attack))
                return NodeStatus.Failure;

            float progress = _predictionDuration <= 0f ? 1f :
                Mathf.Clamp01((Time.time - _predictionStartTime) / _predictionDuration);
            view.SetProgress(progress, _attack);
            if (progress < 1f)
                return NodeStatus.Running;

            if (!ActivateAttack())
                return NodeStatus.Failure;
        }

        return Time.time < _attackFinishTime ? NodeStatus.Running : NodeStatus.Success;
    }

    private bool StartPrediction()
    {
        var view = _blackBoard?.PredictionView;
        var agent = _blackBoard?.NavMeshAgent;
        var owner = _blackBoard?.Transform;
        if (_range == null || view == null || !view.isActiveAndEnabled || owner == null ||
            agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 origin = owner.position;
        Vector3 ground = origin - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, Mathf.Max(0.01f, _sampleRadius), filter))
            return false;

        origin.y = hit.position.y;
        _attack = new AttackContext { Pose = new AttackPose(origin, owner.forward), Range = _range.Range };
        if (!view.Show(_attack.Range, _attack.Pose, _attack))
            return false;

        _attack.HasPrediction = true;
        _predictionStartTime = Time.time;
        _phase = Phase.Prediction;
        return true;
    }

    /// <summary>
    /// 攻撃処置
    /// </summary>
    private bool ActivateAttack()
    {
        var target = _blackBoard.Target;
        Character character = null;
        if (target != null && _attack.Range.Contains(_attack.Pose.Origin, _attack.Pose.Forward, target.transform.position))
        {
            character = target.GetComponent<CharacterComposition>()?.Character;
            if (character == null)
            {
                Debug.LogWarning("範囲攻撃の対象に初期化済みのCharacterCompositionがありません。", target);
                return false;
            }
        }

        // 通知から再評価されても発動・ダメージを繰り返さないよう、先に状態を確定する。
        _phase = Phase.Attack;
        _attackFinishTime = Time.time + Mathf.Max(0f, _attackDuration);
        _attack.DamageApplied = true;
        _blackBoard.PredictionView.Hide(_attack);

        if (_effectPrefab != null)
        {
            _effectInstance = Instantiate(_effectPrefab, _attack.Pose.Origin,
                Quaternion.LookRotation(_attack.Pose.Forward, Vector3.up));
            Destroy(_effectInstance, Mathf.Max(0f, _attackDuration));
        }

        if (character != null && character.Health.CurrentValue > 0)
        {
            int damage = Mathf.RoundToInt(Damage);
            character.TakeDamage(damage);
            Debug.Log($"範囲攻撃：{damage}ダメージ", target);
        }
        return true;
    }

    private void OnDestroy() => FinishAttack();

    /// <summary>
    /// 終了時の処理
    /// </summary>
    private void FinishAttack()
    {
        if (_attack != null)
            _blackBoard?.PredictionView?.Hide(_attack);
        if (_effectInstance != null)
            Destroy(_effectInstance);
        _effectInstance = null;
        _attack = null;
        _phase = Phase.Idle;
    }
}
