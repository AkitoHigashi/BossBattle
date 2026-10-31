using FishNet.Object;
using UnityEngine;
using NCG.Character;
using System.Collections.Generic;

namespace NCG.Player.Spawn
{
    /// <summary>キャラクタープレハブのデータを格納するSO</summary>
    [CreateAssetMenu(fileName = "CharacterDataList", menuName = "NCG/Player/CharacterDataList")]
    public sealed class CharacterDataList : ScriptableObject
    {
        [SerializeField] Dictionary<CharacterType, NetworkObject> _characterDataDict;

        /// <summary>
        /// 選択したキャラクターの種類に応じたプレイヤーのプレハブを取得するメソッド
        /// </summary>
        /// <param name="characterType">選択したキャラクターの種類</param>
        /// <param name="prefab">プレイヤーのプレハブ</param>
        /// <returns>プレイヤーのプレハブを取得できたか</returns>
        public bool TryGetPlayerPrefab(CharacterType characterType, out NetworkObject prefab)
        {
            return _characterDataDict.TryGetValue(characterType, out prefab);
        }
    }
}
