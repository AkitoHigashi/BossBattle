using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーキャラクターの生成を管理するクラス</summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("キャラクターのリスト")] CharacterDataList _characterDataList;

        /// <summary>
        /// プレイヤーを生成するメソッド
        /// </summary>
        /// <param name="playerInfo">プレイヤーの情報</param>
        public void SpawnPlayer(PlayerInfo playerInfo)
        {
            // プレハブ取得
            var prefab = _characterDataList?.GetPlayerPrefab(playerInfo.CharacterType);
        }
    }
}
