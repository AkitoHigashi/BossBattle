
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     ServerRpcを使用してプレイヤーの移動を検証するクラス
/// </summary>
public class NetworkPlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _moveDistance = 0.5f;

    /// <summary>
    ///     所有者だけが入力を取得する
    /// </summary>
    private void Update()
    {
        if (!IsClientInitialized || !IsOwner)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        Vector3 direction = Vector3.zero;

        if (keyboard.wKey.wasPressedThisFrame)
        {
            direction = Vector3.forward;
        }
        else if (keyboard.sKey.wasPressedThisFrame)
        {
            direction = Vector3.back;
        }
        else if (keyboard.aKey.wasPressedThisFrame)
        {
            direction = Vector3.left;
        }
        else if (keyboard.dKey.wasPressedThisFrame)
        {
            direction = Vector3.right;
        }

        if (direction == Vector3.zero)
        {
            return;
        }

        MoveServerRpc(direction);
    }

    /// <summary>
    ///     サーバー上でプレイヤーの移動を実行する
    ///     ServerRpcはクライアントからサーバーにリクエストを送信するためのメソッドであり、サーバー側でのみ実行される
    /// </summary>
    /// <param name="direction"> 移動方向 </param>
    [ServerRpc]
    private void MoveServerRpc(Vector3 direction)
    {
        // クライアントから送られた方向を検証する
        if (direction.sqrMagnitude < 0.99f ||
            direction.sqrMagnitude > 1.01f)
        {
            return;
        }

        transform.position += direction * _moveDistance;

        Debug.Log(
            $"ServerRpc移動 | ObjectId: {ObjectId} | " +
            $"Position: {transform.position}"
        );
    }
}
