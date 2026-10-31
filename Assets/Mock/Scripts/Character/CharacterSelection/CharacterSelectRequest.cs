using FishNet.Broadcast;

namespace NCG.Character.Selection
{
    /// <summary>キャラクター選択のリクエストをするときの情報を持つ構造体</summary>
    public readonly struct CharacterSelectRequest : IBroadcast
    {
        /// <summary>選択したキャラクターの種類キャラクターの種類</summary>
        public readonly CharacterType CharacterType;

        public CharacterSelectRequest(CharacterType characterType)
        {
            CharacterType = characterType;
        }
    }
}
