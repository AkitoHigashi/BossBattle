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

    private const float FullCircleDegrees = 360f;
    private const float MinAttackRadius = 0.01f;

    // 描画する線の角度間隔。小さいほど細かくなる。
    private const float OutlineStepDegrees = 5f;
    private const float FillStepDegrees = 4f;

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

    protected override void DrawTelegraph(float progress)
    {
        var owner = _blackBoard?.Transform;
        if (owner == null) return;

        float maxRadius = Mathf.Max(0.01f, _attackRange);

        DrawSector(owner, maxRadius);

        //一定速度じゃないけどモックだから許してちょ
        DrawFilledSector(owner, maxRadius * Mathf.Clamp01(progress));
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

        float radius = Mathf.Max(MinAttackRadius, _attackRange);

        // 半径外なら命中しない
        if (offset.sqrMagnitude > radius * radius)
            return false;

        float angle = Mathf.Clamp(_arcAngle, 0f, FullCircleDegrees);

        if (angle >= FullCircleDegrees)
            return true;

        Vector3 forward = Vector3.ProjectOnPlane(
            owner.forward, Vector3.up).normalized;

        return Vector3.Angle(forward, offset) <= angle * 0.5f;
    }

    /// <summary>
    /// 攻撃範囲を表示する
    /// </summary>
    /// <param name="owner">円弧の中心にしたいTransform</param>
    /// <param name="radius">半径</param>
    private void DrawSector(Transform owner, float radius)
    {
        Vector3 center = owner.position;
        // 正面方向
        Vector3 forward = Vector3.ProjectOnPlane(
            owner.forward, Vector3.up).normalized;

        float angle = Mathf.Clamp(_arcAngle, 0f, FullCircleDegrees);
        float halfAngle = angle * 0.5f;

        int segments = Mathf.Max(
    1, Mathf.CeilToInt(angle / OutlineStepDegrees));

        // 最初の頂点は中心から左端の外周に向かう線の先端
        Vector3 previous = center
            + Quaternion.AngleAxis(-halfAngle, Vector3.up)
            * forward * radius;

        // 円形の場合、中心から外周への線は描かない。
        if (angle < FullCircleDegrees)
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

        if (angle < FullCircleDegrees)
            Debug.DrawLine(center, previous, Color.yellow);
    }

    /// <summary>
    /// 中心から進捗に応じた距離まで線を引く
    /// </summary>
    /// <param name="owner">円弧の中心にしたいTransform</param>
    /// <param name="radius">半径</param>
    private void DrawFilledSector(Transform owner, float radius)
    {
        if (radius <= 0f)
            return;

        Vector3 center = owner.position;

        Vector3 forward = Vector3.ProjectOnPlane(
            owner.forward, Vector3.up).normalized;

        float angle = Mathf.Clamp(_arcAngle, 0f, FullCircleDegrees);

        int segments = Mathf.Max(
            1, Mathf.CeilToInt(angle / FillStepDegrees));


        for (int i = 0; i <= segments; i++)
        {
            float yaw = -angle * 0.5f + angle * i / segments;

            Vector3 direction =
                Quaternion.AngleAxis(yaw, Vector3.up) * forward;

            Debug.DrawLine(
                center,
                center + direction * radius,
                Color.red);
        }
    }

}
