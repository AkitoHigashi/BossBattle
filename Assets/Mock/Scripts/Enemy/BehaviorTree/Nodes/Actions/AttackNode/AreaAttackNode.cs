using UnityEngine;

/// <summary>
/// 扇型の範囲の攻撃ノード、円形も対応
/// </summary>
[CreateAssetMenu(fileName = "AreaAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AreaAttackNode")]
public class AreaAttackNode : AttackNode
{
    [SerializeField,Range(0f, 360f)] 
    private float _arcAngle = 60f;
    
    [SerializeField,Min(0.01f)] 
    private float _attackRange = 5f;

    protected override void ExecuteAttack()
    {
        if (_blackBoard?.Transform == null)
            return;

        var owner = _blackBoard.Transform;

        var target = _blackBoard.Target;
        if (target == null)
            return;

        if (!IsInRange(owner, target.transform.position))
            return;

        var composition = target.GetComponent<CharacterComposition>();
        if (composition == null)
        {
            Debug.LogWarning(
                "targetにCharacterCompositionがありません。");
            return;
        }

        var character = composition.Character;
        if (character == null || character.Health.CurrentValue <= 0)
            return;

        ApplyDamage(character);
    }

    protected override void DrawTelegraph()
    {
        if (_blackBoard?.Transform != null)
            DrawSector(_blackBoard.Transform);
    }

    /// <summary>
    /// 指定された位置が攻撃範囲内かどうかを判定する。
    /// </summary>
    /// <param name="owner">攻撃者</param>
    /// <param name="targetPosition">対象の位置</param>
    /// <returns>範囲内だったらtrue</returns>
    private bool IsInRange(Transform owner, Vector3 targetPosition)
    {
        Vector3 offset = targetPosition - owner.position;
        offset.y = 0f;

        float radius = Mathf.Max(0.01f, _attackRange);

        // 半径外なら命中しない
        if (offset.sqrMagnitude > radius * radius)
            return false;

        // 中心と重なっている場合は命中する。
        if (offset.sqrMagnitude < 0.000001f)
            return true;

        float angle = Mathf.Clamp(_arcAngle, 0f, 360f);

        if (angle >= 360f)
            return true;

        Vector3 forward = Vector3.ProjectOnPlane(
            owner.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.000001f)
            forward = Vector3.forward;

        return Vector3.Angle(forward, offset) <= angle * 0.5f;
    }

    /// <summary>
    /// 攻撃範囲をデバッグ表示する。円弧の中心はownerの位置、正面はownerの正面に対応する。
    /// </summary>
    /// <param name="owner"></param>
    private void DrawSector(Transform owner)
    {
        Vector3 center = owner.position;
        // 正面方向
        Vector3 forward = Vector3.ProjectOnPlane(
            owner.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.000001f)
            forward = Vector3.forward;

        float radius = Mathf.Max(0.01f, _attackRange);
        float angle = Mathf.Clamp(_arcAngle, 0f, 360f);
        float halfAngle = angle * 0.5f;
        // 5度ごとに分割する
        int segments = Mathf.Max(1, Mathf.CeilToInt(angle / 5f));

        // 最初の頂点は中心から左端の外周に向かう線の先端
        Vector3 previous = center
            + Quaternion.AngleAxis(-halfAngle, Vector3.up)
            * forward * radius;

        // 円形の場合、中心から外周への線は描かない。
        if (angle < 360f)
            Debug.DrawLine(center, previous, Color.yellow);

        // 外周の頂点を順に描画する
        for (int i = 1; i <= segments; i++)
        {
            float yaw = -halfAngle + angle * i / segments;

            Vector3 next = center
                + Quaternion.AngleAxis(yaw, Vector3.up)
                * forward * radius;

            Debug.DrawLine(previous, next, Color.yellow);
            previous = next;
        }

        if (angle < 360f)
            Debug.DrawLine(center, previous, Color.yellow);
    }

}
