using FishNet;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.Events;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーの生成に関するクラス</summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField, Tooltip("プレイヤーキャラクターの生成イベント")] UnityEvent<PlayerInfo> OnSpawnPlayer;

        private void Awake()
        {
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        }

        /// <summary>
        /// クライアントの参加シーンが変わった時に呼ばれるメソッド
        /// </summary>
        /// <param name="args">クライアントの情報</param>
        void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            // シーンから退出した場合はreturn
            if (!args.Added) return;

            // 参加したシーンがこのゲームオブジェクトとは異なったらreturn
            if (args.Scene != gameObject.scene) return;

            // サーバーが立ち上がっていなければreturn
            if (!InstanceFinder.ServerManager.Started) return;

            // プレイヤー情報の生成
            var playerInfo = new PlayerInfo(args.Connection, CharacterType.CharacterA);
            OnSpawnPlayer?.Invoke(playerInfo);
        }
    }
}
