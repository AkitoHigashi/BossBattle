
using System.Collections.Generic;

public readonly struct HitResult
{
    public HitResult(HitResult hitInfo)
    {
        characters = hitInfo.characters;  
    }
    public HitResult(IReadOnlyList<Character> character)
    {
        characters = character;
    }
    
    public IReadOnlyList<Character>  characters { get; }
}