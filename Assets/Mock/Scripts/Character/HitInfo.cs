

public readonly struct HitInfo
{
    public HitInfo(HitInfo hitInfo)
    {
        Target = hitInfo.Target;
    }

    public HitInfo(Character character)
    {
        Target = character;
    }


    public Character Target { get; }
}