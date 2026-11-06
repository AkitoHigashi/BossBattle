using System;
using System.Collections.Generic;
namespace NetworkTest_Yajima
{
    public interface ILobbyCharacterSelection
    {
        event Action<IReadOnlyDictionary<int, string>> Changed;
        void Select(int character);
        void RequestList();
    }
}
