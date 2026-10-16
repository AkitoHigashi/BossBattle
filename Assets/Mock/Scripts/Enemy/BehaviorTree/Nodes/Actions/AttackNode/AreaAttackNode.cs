using UnityEngine;

/// <summary>
/// 扇型の範囲の攻撃ノード、円形も対応
/// </summary>
[CreateAssetMenu(fileName = "AreaAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AreaAttackNode")]
public class AreaAttackNode : AttackNode, IRangeAttackNode
{
    [SerializeField]
    private SectorRange _range = new SectorRange(5f, 60f);

    public SectorRange Range => _range;

    /// <summary>この攻撃が持つ範囲設定を使って、指定位置を判定する。</summary>
    public bool IsInAttackRange(
        Vector3 origin,
        Vector3 forward,
        Vector3 targetPosition)
    {
        return _range.Contains(origin, forward, targetPosition);
    }

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

        if (!IsInAttackRange(
                owner.position,
                owner.forward,
                target.transform.position))
        {
            return;
        }

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

    /// <summary>
    /// 外側の輪郭と、中心から進捗に応じた距離までの線を描画する
    /// </summary>
    /// <param name="progress"></param>
    protected override void DrawTelegraph(float progress)
    {
        var owner = _blackBoard?.Transform;
        if (owner == null)
            return;

        DrawSector(owner, _range.Radius);

        DrawFilledSector(
            owner,
            _range.Radius * Mathf.Clamp01(progress));
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
        Vector3 forward =
         SectorRange.GetHorizontalForward(owner.forward);

        float angle = _range.Angle;
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

        Vector3 forward =
        SectorRange.GetHorizontalForward(owner.forward);

        float angle = _range.Angle;

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
