using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Client Authorityでプレイヤーの移動を同期するテスト用クラス
/// </summary>
public class NetworkPlayerMovementClient : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;

    /// <summary>
    ///     所有者の入力を取得してローカルで移動させる
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

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.aKey.isPressed)
        {
            input.x -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            input.x += 1f;
        }

        // 斜め移動で速度が速くならないように正規化する
        Vector2 normalizedInput = Vector2.ClampMagnitude(input, 1f);

        Vector3 direction = new Vector3(
            normalizedInput.x,
            0f,
            normalizedInput.y
        );

        // ServerRpcを使用せず、所有者の環境で直接移動する
        transform.position += direction * _moveSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            Debug.Log(
                $"Local移動 | IsOwner: {IsOwner} | " +
                $"IsServer: {IsServerInitialized} | " +
                $"Position: {transform.position}"
            );
        }
    }
}
