using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>円・扇形・ドーナツ型の予兆を独立して表示する。当たり判定は行わない。</summary>
[DisallowMultipleComponent]
public sealed class AttackPredictionView : MonoBehaviour
{
    [SerializeField] private Material _material;
    [SerializeField, Min(0f)] private float _surfaceOffset = 0.05f;
    [SerializeField, Min(0.01f)] private float _lineWidth = 0.06f;
    [SerializeField] private Color _outlineColor = new Color(1f, 0.8f, 0.1f, 1f);
    [SerializeField] private Color _fillColor = new Color(1f, 0.15f, 0.05f, 0.35f);

    /// <summary>
    /// 予兆を出すのに必要な情報
    /// </summary>
    private sealed class PredictionVisual
    {
        public GameObject Root;
        public Transform FillTransform;
        public Mesh FillMesh;
        public LineRenderer Outline;
        public LineRenderer InnerOutline;
        public MeshRenderer FillRenderer;
        public Vector3[] Directions;
        public Vector3[] FillVertices;
        public float InnerRadius;
        public float OuterRadius;
        public float Progress;
    }

    private readonly object _defaultKey = new object();
    private readonly Dictionary<object, PredictionVisual> _visible = new Dictionary<object, PredictionVisual>();
    private readonly Stack<PredictionVisual> _pool = new Stack<PredictionVisual>();
    private readonly List<PredictionVisual> _visuals = new List<PredictionVisual>();

    public bool IsVisible => _visible.Count > 0;
    public int VisibleCount => _visible.Count;
    public bool IsVisibleFor(object predictionKey) => _visible.ContainsKey(predictionKey ?? _defaultKey);
    public float Progress => GetProgress();

    /// <summary>
    /// 範囲を表示する関数
    /// </summary>
    /// <param name="range">範囲</param>
    /// <param name="pose">攻撃するポジション</param>
    /// <param name="telegraphKey">どの攻撃なのかの識別子</param>
    /// <returns></returns>
    public bool Show(SectorRange range, AttackPose pose, object telegraphKey = null)
    {
        if (!isActiveAndEnabled)
            return false;
        if (_material == null)
        {
            Debug.LogError("AttackPredictionViewに予測表示用のMaterialを設定してください。", this);
            return false;
        }

        object key = telegraphKey ?? _defaultKey;
        if (!_visible.TryGetValue(key, out var visual) || visual.Root == null)
        {
            visual = TakeFromPool() ?? CreateRenderers();
            _visible[key] = visual;
        }

        // 表示全体の位置と向きを決める
        visual.Root.transform.SetPositionAndRotation(
            pose.Origin + Vector3.up * _surfaceOffset,
            Quaternion.LookRotation(pose.Forward, Vector3.up));

        //曲線を何分割するか
        int segments = Mathf.Max(1, Mathf.CeilToInt(range.Angle / 5f));

        var arc = new Vector3[segments + 1];
        var innerArc = new Vector3[segments + 1];
        visual.Directions = new Vector3[segments + 1];
        visual.InnerRadius = range.InnerRadius;
        visual.OuterRadius = range.Radius;

        //内周と外周の点を作るループ
        for (int i = 0; i <= segments; i++)
        {
            float yaw = -range.Angle * 0.5f + range.Angle * i / segments;
            var direction = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
            visual.Directions[i] = direction;
            arc[i] = direction * range.Radius;
            innerArc[i] = direction * range.InnerRadius;
        }

        bool isCircle = range.Angle >= 360f;
        var outline = new Vector3[arc.Length + (isCircle ? 0 : 2)];
        System.Array.Copy(arc, 0, outline, isCircle ? 0 : 1, arc.Length);

        if (!isCircle)
        {
            outline[0] = innerArc[0];
            outline[outline.Length - 1] = innerArc[innerArc.Length - 1];
        }

        //輪郭線の描画
        visual.Outline.positionCount = outline.Length;
        visual.Outline.SetPositions(outline);

        ConfigureOutline(visual.Outline);

        visual.InnerOutline.gameObject.SetActive(range.InnerRadius > 0f);
        visual.InnerOutline.positionCount = innerArc.Length;
        visual.InnerOutline.SetPositions(innerArc);
        ConfigureOutline(visual.InnerOutline);

        var vertices = new Vector3[(segments + 1) * 2];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            vertices[i * 2] = innerArc[i];
            vertices[i * 2 + 1] = arc[i];
        }

        for (int i = 0; i < colors.Length; i++)
            colors[i] = _fillColor;

        //使用する頂点番号を設定する
        for (int i = 0; i < segments; i++)
        {
            //使用する頂点の配列番号
            int vertex = i * 2;
            int triangle = i * 6;

            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 3;

            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;
        }

        visual.FillVertices = vertices;
        visual.FillTransform.localScale = Vector3.one;

        //塗り用メッシュに頂点・色・三角形の構成を設定する
        visual.FillMesh.Clear();
        visual.FillMesh.vertices = vertices;
        visual.FillMesh.colors = colors;
        visual.FillMesh.triangles = triangles;
        visual.FillMesh.RecalculateBounds();

        visual.FillRenderer.sharedMaterial = _material;

        //塗りの外周を内周まで戻して
        //まだ広がっていない状態にしてる
        SetProgress(0f, key);

        visual.Root.SetActive(true);
        return true;
    }

    /// <summary>
    /// 現在の進行度を取得する関数
    /// </summary>
    /// <param name="telegraphKey">どの攻撃なのかの識別子</param>
    /// <returns></returns>
    public float GetProgress(object telegraphKey = null)
    {
        return _visible.TryGetValue(
            telegraphKey ?? _defaultKey, out var visual) 
            ? visual.Progress : 0f;
    }

    /// <summary>
    /// 進行度に合わせて塗る関数
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="telegraphKey"></param>
    public void SetProgress(float progress, object telegraphKey = null)
    {
        if (!_visible.TryGetValue(telegraphKey ?? _defaultKey, out var visual))
            return;

        visual.Progress = Mathf.Clamp01(progress);
        float radius = Mathf.Lerp(visual.InnerRadius, visual.OuterRadius, visual.Progress);
        for (int i = 0; i < visual.Directions.Length; i++)
            visual.FillVertices[i * 2 + 1] = visual.Directions[i] * radius;
        visual.FillMesh.vertices = visual.FillVertices;
        visual.FillMesh.RecalculateBounds();
    }

    /// <summary>指定した予兆だけを消す</summary>
    public void Hide(object telegraphKey = null)
    {
        object key = telegraphKey ?? _defaultKey;
        if (!_visible.TryGetValue(key, out var visual))
            return;

        _visible.Remove(key);
        Recycle(visual);
    }

    /// <summary>この敵の全ての予兆を消す</summary>
    public void HideAll()
    {
        foreach (var visual in _visible.Values)
            Recycle(visual);
        _visible.Clear();
    }

    private void Recycle(PredictionVisual visual)
    {
        // 表示はシーン直下に別のGameObjectとして作っているので、Play終了やシーン切り替えの時は
        // この敵より先に破棄されていることがある。破棄済みならプールにも戻さない
        if (visual.Root == null)
            return;

        visual.Root.SetActive(false);
        visual.Progress = 0f;
        _pool.Push(visual);
    }

    /// <summary>
    /// プールから再利用できる表示を取り出す。破棄済みのものは捨てる
    /// </summary>
    /// <returns>再利用できる表示。無ければ null</returns>
    private PredictionVisual TakeFromPool()
    {
        while (_pool.Count > 0)
        {
            var visual = _pool.Pop();
            if (visual.Root != null)
                return visual;
        }

        return null;
    }

    /// <summary>
    /// 予兆一つ分の表示を作る
    /// </summary>
    /// <returns></returns>
    private PredictionVisual CreateRenderers()
    {
        var visual = new PredictionVisual();
        visual.Root = new GameObject($"Attack Prediction ({name})");
        visual.Root.layer = gameObject.layer;
        visual.Root.SetActive(false);
        visual.Outline = visual.Root.AddComponent<LineRenderer>();
        visual.Outline.useWorldSpace = false;
        // 俯瞰・斜め視点のどちらでも輪郭を確認できる。
        visual.Outline.alignment = LineAlignment.View;
        visual.Outline.shadowCastingMode = ShadowCastingMode.Off;
        visual.Outline.receiveShadows = false;

        var innerOutline = new GameObject("Inner Outline", typeof(LineRenderer));
        innerOutline.layer = gameObject.layer;
        innerOutline.transform.SetParent(visual.Root.transform, false);
        visual.InnerOutline = innerOutline.GetComponent<LineRenderer>();
        visual.InnerOutline.useWorldSpace = false;
        visual.InnerOutline.alignment = LineAlignment.View;
        visual.InnerOutline.shadowCastingMode = ShadowCastingMode.Off;
        visual.InnerOutline.receiveShadows = false;

        var fill = new GameObject("Progress Fill", typeof(MeshFilter), typeof(MeshRenderer));
        fill.layer = gameObject.layer;
        fill.transform.SetParent(visual.Root.transform, false);
        visual.FillTransform = fill.transform;
        visual.FillMesh = new Mesh { name = "Attack Prediction Fill" };
        fill.GetComponent<MeshFilter>().sharedMesh = visual.FillMesh;
        visual.FillRenderer = fill.GetComponent<MeshRenderer>();
        visual.FillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        visual.FillRenderer.receiveShadows = false;
        _visuals.Add(visual);
        return visual;
    }

    private void ConfigureOutline(LineRenderer outline)
    {
        outline.widthMultiplier = Mathf.Max(0.01f, _lineWidth);
        outline.startColor = _outlineColor;
        outline.endColor = _outlineColor;
        outline.sharedMaterial = _material;
    }

    private void OnDisable() => HideAll();

    private void OnDestroy()
    {
        foreach (var visual in _visuals)
        {
            if (visual.Root != null)
                Destroy(visual.Root);
            if (visual.FillMesh != null)
                Destroy(visual.FillMesh);
        }
        _visible.Clear();
        _pool.Clear();
        _visuals.Clear();
    }
}
