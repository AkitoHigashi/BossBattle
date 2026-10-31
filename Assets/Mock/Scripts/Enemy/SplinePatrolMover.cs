using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// スプラインに沿って巡回し続けるテスト用コンポーネント
/// BehaviorTree・BlackBoardとは独立しており、Transformを直接動かす
/// </summary>
[DisallowMultipleComponent]
public sealed class SplinePatrolMover : MonoBehaviour
{
    [SerializeField, Tooltip("巡回するスプライン。閉じていればループ、開いていれば往復する")]
    private SplineContainer _spline;

    [SerializeField, Min(0.01f), Tooltip("移動速度（m/s）")]
    private float _moveSpeed = 3f;

    [SerializeField, Tooltip("スプラインからの高さ。ピボットが中心にあるモデルを地面に乗せるために使う")]
    private float _heightOffset = 1f;

    [SerializeField, Tooltip("進行方向へ向きを合わせるか")]
    private bool _faceMoveDirection = true;

    // スプライン上の位置を0〜1で表す。距離で正規化されているので、速度/全長を足せば等速になる
    private float _t;
    // 往復時の進む向き。1が順方向、-1が逆方向
    private float _direction = 1f;
    // 全長の計算は軽くないので開始時に一度だけ求める（実行中にスプラインを編集しても反映されない）
    private float _length;

    private void Start()
    {
        if (_spline == null || _spline.Spline == null || _spline.Spline.Count < 2)
        {
            Debug.LogError("巡回用のスプラインが未設定か、Knotが2つ未満です。", this);
            enabled = false;// 無効化してUpdateを止める
            return;
        }

        _length = _spline.CalculateLength();

        // 置いた位置から一番近いスプライン上の点から始める。t=0から始めるとワープして見えるため
        // GetNearestPointはスプラインのローカル座標で計算するので、ワールド座標を変換して渡す
        float3 localPosition = _spline.transform.InverseTransformPoint(transform.position);
        SplineUtility.GetNearestPoint(_spline.Spline, localPosition, out _, out _t);

        ApplyPose();
    }

    private void Update()
    {
        if (_length <= 0f)
            return;

        _t += _direction * _moveSpeed * Time.deltaTime / _length;

        if (_spline.Spline.Closed)
        {
            // 閉じたスプラインは終点と始点がつながっているので、はみ出した分だけ巻き戻す
            _t = Mathf.Repeat(_t, 1f);
        }
        else if (_t >= 1f || _t <= 0f)
        {
            // 開いたスプラインは端で折り返す
            _t = Mathf.Clamp01(_t);
            _direction = -_direction;
        }

        ApplyPose();
    }

    /// <summary>
    /// 現在のtに合わせて位置と向きを反映する
    /// </summary>
    private void ApplyPose()
    {
        // SplineContainerのEvaluate系はコンテナのTransformを含んだワールド座標を返す
        Vector3 position = _spline.EvaluatePosition(_t);
        transform.position = position + Vector3.up * _heightOffset;

        if (!_faceMoveDirection)
            return;

        Vector3 forward = (Vector3)_spline.EvaluateTangent(_t) * _direction;
        forward.y = 0f;// 坂道で前のめりにならないよう水平方向だけ見る

        if (forward.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
