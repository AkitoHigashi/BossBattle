using UnityEngine;

/// <summary>地上の予兆の後、範囲内に攻撃してエフェクトを出す</summary>
public sealed class AreaAttackNode : AttackNode
{
    public override string Description => DescriptionText;
    private const string DescriptionText =
        "Time To Attackの間、足元の地面にWarning Areaの予兆を出した後、範囲内のターゲットにダメージを与える。\nエフェクトを設定していれば発動時に生成し、Recovery Timeが過ぎたらSuccess。";

    [SerializeField, Tooltip("発動時に生成し、攻撃終了時に破棄するエフェクト。未設定でも判定を実行する。")]
    private GameObject _effectPrefab;

    private GameObject _effectInstance;

    protected override NodeStatus OnAttack()
    {
        if (_effectPrefab != null)
        {
            _effectInstance = Instantiate(_effectPrefab, Pose.Origin,
                Quaternion.LookRotation(Pose.Forward, Vector3.up));
            Destroy(_effectInstance, RecoveryTime);
        }

        Hit();
        return NodeStatus.Success;
    }

    protected override void OnEnd()
    {
        base.OnEnd();
        if (_effectInstance != null)
            Destroy(_effectInstance);
        _effectInstance = null;
    }

    private void OnDestroy() => OnEnd();
}
