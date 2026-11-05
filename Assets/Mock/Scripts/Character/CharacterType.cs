using UnityEngine;

namespace NCG.Character
{
    /// <summary>プレイヤーが操作するキャラクターの種類を定義した列挙型</summary>
    public enum CharacterType : byte
    {
        [InspectorName("キャラクターA")] CharacterA = 0,
        [InspectorName("キャラクターB")] CharacterB = 1,
        [InspectorName("キャラクターC")] CharacterC = 2,
        [InspectorName("キャラクターD")] CharacterD = 3,
    }
}
