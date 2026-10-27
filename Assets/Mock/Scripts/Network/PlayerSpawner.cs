using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーキャラクターの生成を管理するクラス</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("キャラクターのリスト")] CharacterDataList _characterDataList;

        /// <summary>
        /// プレイヤーを生成するメソッド
        /// </summary>
        /// <param name="playerInfo">プレイヤーの情報</param>
        public void SpawnPlayer(PlayerInfo playerInfo)
        {
            if (_characterDataList == null)
            {
                Debug.LogError("SOがアサインされていません");
                return;
            }

            if (!_characterDataList.TryGetPlayerPrefab(playerInfo.CharacterType, out var prefab))
            {
                Debug.LogError("プレハブを取得できませんでした");
                return;
            }
        }
    }
}
