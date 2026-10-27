using UnityEngine;

namespace NCG.Player.Spawn
{
    /// <summary>プレイヤーが操作するキャラクターの種類を定義した列挙型</summary>
    public enum CharacterType : int
    {
        [InspectorName("不正値")] Invalid = -1,
        [InspectorName("キャラクターA")] CharacterA = 0,
    }
}
