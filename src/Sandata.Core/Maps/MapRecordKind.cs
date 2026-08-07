namespace Sandata.Core.Maps;

/// <summary>
/// The nine record kinds an `.hkmap` file is built from (design section
/// 12). Declared in the order the kinds appear in a well-formed file —
/// header records <see cref="Hkmap"/>, <see cref="Name"/>, and
/// <see cref="Grid"/>, then any number of body records, then
/// <see cref="End"/> — but the *numeric values* are chosen deliberately:
/// <see cref="Wall"/> through <see cref="Objective"/> are pinned to 1
/// through 5, exactly the canonicalisation ordinal design section 12
/// assigns to those five body kinds ("WALL = 1, DOOR = 2, COVER = 3,
/// SPAWN = 4, OBJECTIVE = 5"). That lets a future canonicaliser use
/// <c>(int)kind</c> directly as the sort-key ordinal and the content-hash
/// byte, instead of maintaining a second, parallel mapping that could
/// silently drift from this one. The three kinds the design gives no
/// ordinal for — the two remaining headers and the terminator — are
/// assigned explicit out-of-band values so they can never collide with a
/// body ordinal.
/// </summary>
public enum MapRecordKind
{
    /// <summary>The format-version header. Always line 1.</summary>
    Hkmap = 0,

    /// <summary>The map identifier header.</summary>
    Name = 6,

    /// <summary>The nav grid dimensions header.</summary>
    Grid = 7,

    /// <summary>A wall segment. Canonicalisation ordinal 1.</summary>
    Wall = 1,

    /// <summary>A door segment. Canonicalisation ordinal 2.</summary>
    Door = 2,

    /// <summary>A directional cover object. Canonicalisation ordinal 3.</summary>
    Cover = 3,

    /// <summary>A faction spawn point. Canonicalisation ordinal 4.</summary>
    Spawn = 4,

    /// <summary>A mission objective. Canonicalisation ordinal 5.</summary>
    Objective = 5,

    /// <summary>The terminator record. Exactly one, and it is the last line.</summary>
    End = 8,
}
