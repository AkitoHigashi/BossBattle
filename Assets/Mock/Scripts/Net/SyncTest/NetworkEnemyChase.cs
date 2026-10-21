
using FishNet.Object;
using UnityEngine;

/// <summary>
///     サーバー側だけで動作する敵の追跡AI
/// </summary>
public class NetworkEnemyChase : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _stopDistance = 1.5f;

    /// <summary>
    ///     サーバー側だけでプレイヤーを追跡する
    /// </summary>
    private void Update()
    {
        if (!IsServerInitialized)
        {
            return;
        }

        NetworkPlayerMovementClient target = FindNearestPlayer();

        if (target == null)
        {
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude <= _stopDistance)
        {
            return;
        }

        transform.position +=
            direction.normalized * _moveSpeed * Time.deltaTime;
    }

    /// <summary>
    ///     最も近いプレイヤーを取得する
    /// </summary>
    /// <returns> 最も近いプレイヤー </returns>
    private NetworkPlayerMovementClient FindNearestPlayer()
    {
        NetworkPlayerMovementClient[] players =
            FindObjectsByType<NetworkPlayerMovementClient>();

        NetworkPlayerMovementClient nearestPlayer = null;
        float nearestDistanceSqr = float.MaxValue;

        foreach (NetworkPlayerMovementClient player in players)
        {
            if (!player.IsServerInitialized)
            {
                continue;
            }

            float distanceSqr =
                (player.transform.position - transform.position)
                .sqrMagnitude;

            if (distanceSqr >= nearestDistanceSqr)
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            nearestPlayer = player;
        }

        return nearestPlayer;
    }
}
