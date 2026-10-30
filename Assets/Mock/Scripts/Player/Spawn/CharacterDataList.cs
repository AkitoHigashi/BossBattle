using FishNet.Object;
using System;
using System.Linq;
using UnityEngine;
using NCG.Character;

namespace NCG.Player.Spawn
{
    /// <summary>キャラクタープレハブのデータを格納するSO</summary>
    [CreateAssetMenu(fileName = "CharacterDataList", menuName = "NCG/Player/CharacterDataList")]
    public sealed class CharacterDataList : ScriptableObject
    {
        [SerializeField] CharacterData[] _characterDataArray;

        /// <summary>
        /// 選択したキャラクターの種類に応じたプレイヤーのプレハブを取得するメソッド
        /// </summary>
        /// <param name="characterType">選択したキャラクターの種類</param>
        /// <param name="prefab">プレイヤーのプレハブ</param>
        /// <returns>プレイヤーのプレハブを取得できたか</returns>
        public bool TryGetPlayerPrefab(CharacterType characterType, out NetworkObject prefab)
        {
            prefab = _characterDataArray.FirstOrDefault(c => c.CharacterType == characterType)?.PlayerPrefab;
            return prefab != null;
        }

        [Serializable]
        class CharacterData
        {
            [SerializeField, Tooltip("キャラクターの種類")] CharacterType _characterType;
            [SerializeField, Tooltip("プレハブ")] NetworkObject _playerPrefab;

            public CharacterType CharacterType => _characterType;
            public NetworkObject PlayerPrefab => _playerPrefab;
        }
    }
}
