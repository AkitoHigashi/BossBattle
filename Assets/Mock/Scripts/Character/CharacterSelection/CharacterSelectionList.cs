using FishNet.Broadcast;

namespace NCG.Character.Selection
{
    /// <summary>キャラクター選択状況一覧を持つ構造体</summary>
    public readonly struct CharacterSelectionList : IBroadcast
    {
        /// <summary>確定したキャラクター選択状況一覧</summary>
        public readonly CharacterSelectionEntry[] Entries;

        public CharacterSelectionList(CharacterSelectionEntry[] entries)
        {
            Entries = entries.Clone() as CharacterSelectionEntry[];
        }
    }
}
