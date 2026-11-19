using UnityEngine;
using UnityEngine.Rendering;

/// <summary>ボスの足元で、連撃が進む方向を円弧の矢印で表すクラス</summary>
public sealed class SwirlWarningView : MonoBehaviour
{
    private const int ArrowCount = 3;
    // 線の角を滑らかにするための頂点数
    private const int CornerVertexCount = 2;

    // 線の両端を丸くするための頂点数
    private const int CapVertexCount = 2;
    /// <summary>
    /// 1つの円弧を何分割して描くか
    /// </summary>
    private const int SegmentCount = 16;
    private const float ArcAngle = 68f;

    [SerializeField]
    private Material _material;
    [SerializeField, Min(0.1f),Tooltip("円弧の半径")]
    private float _radius = 3f;
    [SerializeField, Min(0.01f),Tooltip("線の幅")]
    private float _lineWidth = 0.12f;
    [SerializeField, Min(0.01f),Tooltip("矢印の大きさ")]
    private float _arrowSize = 0.45f;
    [SerializeField, Min(0f),Tooltip("1秒あたりの回転角度")]
    private float _degreesPerSecond = 180f;
    [SerializeField] private Color _color;

    /// <summary>
    /// 回転方向、1だったら時計回り、-１で反時計
    /// </summary>
    private int _direction;

    /// <summary>
    /// 円弧と矢印を生成する
    /// </summary>
    /// <param name="clockwise">どっちに回るのか</param>
    /// <returns></returns>
    public bool TryShow(bool clockwise)
    {
        if (_material == null)
        {
            Debug.LogError("渦の予兆に表示用のMaterialを設定してください。", this);
            return false;
        }

        _direction = clockwise ? 1 : -1;

        for (int arrow = 0; arrow < ArrowCount; arrow++)
        {
            float endAngle = arrow * (360f / ArrowCount);
            var arc = new Vector3[SegmentCount + 1];

            for (int segment = 0; segment <= SegmentCount; segment++)
            {
                float remaining = 1f - (float)segment / SegmentCount;
                float angle = endAngle - _direction * ArcAngle * remaining;
                arc[segment] = Quaternion.AngleAxis(angle, Vector3.up)
                    * Vector3.forward 
                    * _radius;
            }

            // 円弧の最後の点
            Vector3 tip = arc[SegmentCount];
            //中心から矢印の先端に向かう方向
            Vector3 radial = tip.normalized;
            // 円に沿って進む方向を求める
            Vector3 tangent = _direction * Vector3.Cross(Vector3.up, radial);
            // 矢印の先端から後ろに戻った位置
            Vector3 arrowBase = tip - tangent * _arrowSize;
            // 羽の座標を計算
            Vector3 wing = radial * (_arrowSize * 0.55f);

            //円弧を描画
            DrawLine("Arc", arc);
            // 矢印の先端を描画
            DrawLine("Arrowhead", new[] 
            { arrowBase + wing, tip, arrowBase - wing });
        }

        return true;
    }

    /// <summary>
    /// 経過時間に応じて矢印全体を回転する
    /// </summary>
    /// <param name="elapsed"></param>
    public void SetElapsedTime(float elapsed)
    {
        transform.rotation = Quaternion.AngleAxis(
            _direction * _degreesPerSecond * elapsed,
            Vector3.up);
    }

    /// <summary>
    /// 線の描画
    /// </summary>
    /// <param name="lineName"></param>
    /// <param name="points"></param>
    private void DrawLine(string lineName, Vector3[] points)
    {
        var child = new GameObject(lineName, typeof(LineRenderer));

        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);

        var line = child.GetComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.sharedMaterial = _material;
        line.widthMultiplier = _lineWidth;
        line.startColor = _color;
        line.endColor = _color;

        line.numCornerVertices = CornerVertexCount;
        line.numCapVertices = CapVertexCount;

        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
    }
}
