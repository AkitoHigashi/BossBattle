using UnityEngine;
using UnityEngine.Rendering;

/// <summary>当たり判定を行わず、確定した円・扇形と進行度だけを表示する。</summary>
[DisallowMultipleComponent]
public sealed class AttackPredictionView : MonoBehaviour
{
    [SerializeField] private Material _material;
    [SerializeField, Min(0f)] private float _surfaceOffset = 0.05f;
    [SerializeField, Min(0.01f)] private float _lineWidth = 0.06f;
    [SerializeField] private Color _outlineColor = new Color(1f, 0.8f, 0.1f, 1f);
    [SerializeField] private Color _fillColor = new Color(1f, 0.15f, 0.05f, 0.35f);

    private GameObject _viewRoot;
    private Transform _fillTransform;
    private Mesh _fillMesh;
    private LineRenderer _outline;

    public bool IsVisible => _viewRoot != null && _viewRoot.activeSelf;
    public float Progress { get; private set; }

    public bool Show(SectorRange range, AttackPose pose)
    {
        if (_material == null)
        {
            Debug.LogError("AttackPredictionViewに予測表示用のMaterialを設定してください。", this);
            return false;
        }

        EnsureRenderers();
        // 敵の子にはしない。落下中も地上の確定地点に表示を残す。
        _viewRoot.transform.SetPositionAndRotation(
            pose.Origin + Vector3.up * _surfaceOffset,
            Quaternion.LookRotation(pose.Forward, Vector3.up));

        int segments = Mathf.Max(1, Mathf.CeilToInt(range.Angle / 5f));
        var arc = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float yaw = -range.Angle * 0.5f + range.Angle * i / segments;
            arc[i] = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward * range.Radius;
        }

        bool isCircle = range.Angle >= 360f;
        var outline = new Vector3[arc.Length + (isCircle ? 0 : 2)];
        System.Array.Copy(arc, 0, outline, isCircle ? 0 : 1, arc.Length);
        _outline.positionCount = outline.Length;
        _outline.SetPositions(outline);
        _outline.widthMultiplier = Mathf.Max(0.01f, _lineWidth);
        _outline.startColor = _outlineColor;
        _outline.endColor = _outlineColor;
        _outline.sharedMaterial = _material;

        var vertices = new Vector3[segments + 2];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * 3];
        System.Array.Copy(arc, 0, vertices, 1, arc.Length);
        for (int i = 0; i < colors.Length; i++)
            colors[i] = _fillColor;
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        _fillMesh.Clear();
        _fillMesh.vertices = vertices;
        _fillMesh.colors = colors;
        _fillMesh.triangles = triangles;
        _fillMesh.RecalculateBounds();
        _fillTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;
        SetProgress(0f);
        _viewRoot.SetActive(true);
        return true;
    }

    public void SetProgress(float progress)
    {
        Progress = Mathf.Clamp01(progress);
        if (_fillTransform != null)
            _fillTransform.localScale = new Vector3(Progress, 1f, Progress);
    }

    public void Hide()
    {
        if (_viewRoot != null)
            _viewRoot.SetActive(false);
        Progress = 0f;
    }

    private void EnsureRenderers()
    {
        if (_viewRoot != null)
            return;

        _viewRoot = new GameObject($"Attack Prediction ({name})");
        _viewRoot.layer = gameObject.layer;
        _viewRoot.SetActive(false);
        _outline = _viewRoot.AddComponent<LineRenderer>();
        _outline.useWorldSpace = false;
        // 俯瞰・斜め視点のどちらでも輪郭を確認できる。
        _outline.alignment = LineAlignment.View;
        _outline.shadowCastingMode = ShadowCastingMode.Off;
        _outline.receiveShadows = false;

        var fill = new GameObject("Progress Fill", typeof(MeshFilter), typeof(MeshRenderer));
        fill.layer = gameObject.layer;
        fill.transform.SetParent(_viewRoot.transform, false);
        _fillTransform = fill.transform;
        _fillMesh = new Mesh { name = "Attack Prediction Fill" };
        fill.GetComponent<MeshFilter>().sharedMesh = _fillMesh;
        var renderer = fill.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (_viewRoot != null)
            Destroy(_viewRoot);
        if (_fillMesh != null)
            Destroy(_fillMesh);
    }
}
