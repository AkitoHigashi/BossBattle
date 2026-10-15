using UnityEngine;
[CreateAssetMenu(fileName = "AttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AttackNode")]
public abstract class AttackNode : ActionNode
{
    protected float Damage => _attackMultiplier;
    protected float AttackInterval => _attackInterval;


    /// <summary>
    /// 攻撃処理
    /// </summary>
    protected abstract void ExecuteAttack();


    /// <summary>
    /// 予兆を描画する関数
    /// </summary>
    /// <param name="progress">予兆開始時は0、終了時は1</param>
    protected virtual void DrawTelegraph(float progress) { }

    protected sealed override NodeStatus OnUpdate()
    {
        if (_state == AttackState.Ready)
        {
            _finishTime = Time.time + _telegraphTime;
            _state = AttackState.Telegraph;
        }

        if (_state == AttackState.Telegraph)
        {
            if (Time.time < _finishTime)
            {
                // 予兆描画の進捗を計算する
                float progress = _telegraphTime > 0f
                    ? Mathf.Clamp01(
                        1f - (_finishTime - Time.time) / _telegraphTime)
                    : 1f;

                DrawTelegraph(progress);
                return NodeStatus.Running;
            }

            // 広がり切ったら描画を止め、攻撃する。
            ExecuteAttack();

            _finishTime = Time.time + Mathf.Max(0f, _attackInterval);
            _state = AttackState.Recovery;
        }

        if (Time.time < _finishTime)
            return NodeStatus.Running;

        _state = AttackState.Ready;
        return NodeStatus.Success;
    }

    protected void ApplyDamage(Character target)
    {
        if (target == null) return;

        target.TakeDamage((int)(_blackBoard.AttackDamage * _attackMultiplier));

        Debug.Log($"{target}に{(int)(_blackBoard.AttackDamage * _attackMultiplier)}ダメージ");
    }

    [SerializeField, Min(0.01f)] private float _attackMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float _attackInterval = 1f;
    [SerializeField, Min(0f)] private float _telegraphTime = 1f;
    private float _finishTime;
    private AttackState _state;
    private enum AttackState
    {
        Ready,
        Telegraph,
        Recovery
    }
}
