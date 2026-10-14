using UnityEngine;
[CreateAssetMenu(fileName = "AreaAttackNode", menuName = "Behavior Tree/Mock/Nodes/Action Nodes/AreaAttackNode")]
public class AreaAttackNode : AttackNode
{
    [SerializeField,Range(0f, 360f)] 
    private float _arcAngle = 60f;
    
    [SerializeField,Min(0.01f)] 
    private float _attackRange = 5f;

    protected override NodeStatus OnUpdate()
    {
        if (_blackBoard?.Transform == null)
            return NodeStatus.Failure;

        DrawSector(_blackBoard.Transform);
        return NodeStatus.Success;
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
