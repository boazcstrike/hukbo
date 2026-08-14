using Sandata.Core.Navigation;

namespace Sandata.Client.Audio;

/// <summary>
/// Whether the position (<c>positionX</c>, <c>positionY</c>) sits enclosed by
/// wall geometry — the client's answer to <c>SandataSoundPlayer.HandleShotFired</c>'s
/// <c>shooterIsIndoors</c> parameter, derived from the same baked
/// <see cref="NavGrid"/> and <see cref="WallBuckets"/> <c>SandataGame</c>
/// already builds at load and holds for the life of the mission. Before this
/// type existed, every call site passed a hardcoded <see langword="false"/>,
/// because nothing in <c>Sandata.Core</c> knows which side of a wall an
/// operator is on and the simulation must not be taught — this is a sound
/// choice, not a gameplay one, so it is derived here instead.
/// </summary>
/// <remarks>
/// <para>
/// <b>The predicate.</b> A position counts as indoors when a line-of-sight
/// probe cast from it in every one of eight fixed compass directions (N, NE,
/// E, SE, S, SW, W, NW) hits a wall segment within <see cref="ProbeRangeWu"/>
/// world units. Each probe reuses <see cref="LineOfSight.FirstBlockingSegment"/>
/// exactly as <c>WeaponLoweredRules</c> already reuses the same grid and wall
/// bucket pair for its own proximity test, so this type introduces no new
/// geometry primitive — only a new way of composing the existing one. A probe
/// that hits nothing before <see cref="ProbeRangeWu"/> means open air lies in
/// that direction, so a single missed probe is enough to call the position
/// outdoors; all eight must hit for it to read as enclosed.
/// </para>
/// <para>
/// A closed door's rasterised footprint is never a hit: <see cref="WallBuckets"/>
/// is built from <c>WALL</c> records alone (<c>WeaponLoweredRules</c>'s own
/// remarks state this — a door is tested there through the grid's
/// <see cref="NavCellFlags.Door"/> tag instead, never through the wall bucket
/// index), so a probe cast straight through a doorway keeps travelling past it
/// exactly as if the opening were not there. That is what keeps a doorway from
/// reading as enclosed on the near side, and it is also why a position dead
/// centre under a door counts as less enclosed than one a few world units to
/// either side of it: the doorway itself is open sky to this predicate, closed
/// door or not.
/// </para>
/// <para>
/// <b>This is provisional</b> — a fixed-radius eight-direction silhouette
/// around one point, not a topological inside/outside test. It says "a wall
/// is close in every direction from here," which agrees with "this position
/// is inside a room" for the maps built so far, but it is not the same
/// statement: a deep concave outdoor corner narrower than twice
/// <see cref="ProbeRangeWu"/> would misread as indoors, and a room wider than
/// twice <see cref="ProbeRangeWu"/> would misread as outdoors at its centre.
/// The discriminator that would replace it, if a future map needed one, is one
/// that actually reasons about interior versus exterior over the baked
/// passability grid — a flood fill seeded from a known exterior cell, or an
/// authored per-room tag on the map format — rather than a fixed-radius
/// silhouette around a single point.
/// </para>
/// </remarks>
internal static class IndoorPresence
{
    /// <summary>
    /// The world-unit reach of each direction's line-of-sight probe. Chosen
    /// to clear <c>angle-house.hkmap</c>'s roughly 32 world-unit corridors and
    /// its smallest enclosed rooms (the closed-off room bounded by the walls
    /// at world x = 60 and x = 180, roughly 120 world units apart) while
    /// staying short enough that the bottom room holding both blue spawns —
    /// itself walled on every side, but 640 world units wide — does not read
    /// as enclosed from a spawn point close to only one or two of its walls.
    /// A future map with rooms outside this size range would need a different
    /// value, or the topological successor this type's remarks describe.
    /// </summary>
    internal const long ProbeRangeWu = 96;

    /// <summary>
    /// The eight compass directions a probe is cast in, as unit steps applied
    /// to <see cref="ProbeRangeWu"/>. Order does not affect the result — every
    /// direction must hit for <see cref="IsIndoors"/> to return
    /// <see langword="true"/> — but a fixed, named order keeps the probe set
    /// itself reviewable rather than assembled inline.
    /// </summary>
    private static readonly (long DirectionX, long DirectionY)[] Directions =
    [
        (0, -1),  // N
        (1, -1),  // NE
        (1, 0),   // E
        (1, 1),   // SE
        (0, 1),   // S
        (-1, 1),  // SW
        (-1, 0),  // W
        (-1, -1), // NW
    ];

    /// <summary>
    /// Whether (<paramref name="positionX"/>, <paramref name="positionY"/>)
    /// reads as enclosed by wall geometry under this type's predicate — see
    /// the type remarks. A position whose own cell falls outside
    /// <paramref name="grid"/>'s bounds is never indoors: there is no wall
    /// data to enclose it, and probing from an out-of-bounds origin would
    /// otherwise throw inside <see cref="GridRay.Traverse"/>.
    /// </summary>
    /// <param name="positionX">The query position's X coordinate, in whole world units.</param>
    /// <param name="positionY">The query position's Y coordinate, in whole world units.</param>
    /// <param name="grid">
    /// The baked navigation grid supplying bounds for every probe.
    /// </param>
    /// <param name="wallBuckets">
    /// The wall segment index built over the same map <paramref name="grid"/>
    /// is baked from — the same pair <c>SandataGame</c> already holds as
    /// <c>_navGrid</c> and <c>_wallBuckets</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="grid"/> or <paramref name="wallBuckets"/> is
    /// <see langword="null"/>.
    /// </exception>
    internal static bool IsIndoors(
        long positionX,
        long positionY,
        NavGrid grid,
        WallBuckets wallBuckets)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        var originCellX = NavGrid.WorldToCellCoordinate(positionX);
        var originCellY = NavGrid.WorldToCellCoordinate(positionY);

        if (!grid.IsInBounds(originCellX, originCellY))
        {
            return false;
        }

        foreach (var (directionX, directionY) in Directions)
        {
            var targetX = positionX + (directionX * ProbeRangeWu);
            var targetY = positionY + (directionY * ProbeRangeWu);

            var blockingSegment = LineOfSight.FirstBlockingSegment(
                positionX, positionY, targetX, targetY, grid, wallBuckets);

            if (blockingSegment < 0)
            {
                return false;
            }
        }

        return true;
    }
}
