﻿namespace Core;

public enum CursorType
{
    None = 0,
    Kill = 1,
    Loot = 2,
    Skin = 3,
    Mine = 4,
    Herb = 5,
    Vendor = 6,
    Repair = 7,
    Innkeeper = 8,
    Quest = 9
    // todo salvage icon
}

public static class CursorType_Extension
{
    public static string ToStringF(this CursorType value) => value switch
    {
        CursorType.None => nameof(CursorType.None),
        CursorType.Kill => nameof(CursorType.Kill),
        CursorType.Loot => nameof(CursorType.Loot),
        CursorType.Skin => nameof(CursorType.Skin),
        CursorType.Mine => nameof(CursorType.Mine),
        CursorType.Herb => nameof(CursorType.Herb),
        CursorType.Vendor => nameof(CursorType.Vendor),
        CursorType.Repair => nameof(CursorType.Repair),
        CursorType.Innkeeper => nameof(CursorType.Innkeeper),
        CursorType.Quest => nameof(CursorType.Quest),
        _ => nameof(CursorType.None)
    };
}