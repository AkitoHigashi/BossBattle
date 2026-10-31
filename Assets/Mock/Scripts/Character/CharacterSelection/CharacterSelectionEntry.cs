using FishNet.Broadcast;

namespace NCG.Character.Selection
{
    /// <summary>各プレイヤーのキャラクター選択状況を持つ構造体</summary>
    public readonly struct CharacterSelectionEntry : IBroadcast
    {
        /// <summary>キャラクター選択をしたプレイヤーのID</summary>
        public readonly int ClientId;

        /// <summary>選択したキャラクターの種類</summary>
        public readonly CharacterType CharacterType;

        public CharacterSelectionEntry(int clientId, CharacterType characterType)
        {
            ClientId = clientId;
            CharacterType = characterType;
        }
    }
}
