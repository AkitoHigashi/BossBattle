
using FishNet.Object;
using UnityEngine;

/// <summary>
///     ネットワークプレイヤーの所有権を確認するクラス
/// </summary>
public class NetworkPlayerOwnershipTest : NetworkBehaviour
{
    /// <summary>
    ///     クライアントでネットワーク初期化が完了した際に呼び出される
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log(
            $"Player生成完了 | ObjectId: {ObjectId} | " +
            $"OwnerId: {OwnerId} | IsOwner: {IsOwner}"
        );

        if (IsOwner)
        {
            Debug.Log("このCubeは自分が所有しています。");
        }
    }
}
