namespace NCG.Character.Selection
{
    /// <summary>キャラクターの種類を獲得する機能を持つインターフェース</summary>
    public interface IPlayerCharacterGettable
    {
        /// <summary>キャラクターの種類を獲得するメソッド</summary>
        /// <param name="characterType">キャラクターの種類</param>
        /// <returns>獲得できたか</returns>
        bool TryGetCharacterType(out CharacterType characterType);
    }
}
