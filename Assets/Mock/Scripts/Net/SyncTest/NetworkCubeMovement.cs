using FishNet.Object;
using UnityEngine;

/// <summary>
///     サーバー側でCubeを左右に移動させるテスト用クラス
/// </summary>
public class NetworkCubeMovement : NetworkBehaviour
{
    [SerializeField] private float _moveRange = 3f;
    [SerializeField] private float _moveSpeed = 2f;

    private Vector3 _initialPosition;

    /// <summary>
    ///     サーバー側でネットワークオブジェクトが初期化されているときに呼ばれるメソッド
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        _initialPosition = transform.position;
    }

    private void Update()
    {
        // サーバーだけが移動処理を実行する
        // IsServerInitializedはサーバー側で初期化されているかどうかを示す
        if (!IsServerInitialized)
        {
            return;
        }

        float offset = Mathf.Sin(Time.time * _moveSpeed) * _moveRange;

        transform.position = _initialPosition + Vector3.right * offset;
    }
}