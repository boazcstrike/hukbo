using Sandata.Client.Audio;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;

namespace Sandata.Client.Tests;

/// <summary>
/// Proves <see cref="IndoorPresence.IsIndoors"/> against the real
/// <c>angle-house.hkmap</c> geometry (embedded here rather than read from
/// <c>tests/Sandata.Core.Tests/Fixtures</c>, since this test project holds no
/// fixture of its own and this class' scope is limited to one new file): both
/// blue spawns, which sit in the open bottom room rather than any enclosed
/// space, must read as outdoors; a position inside the map's one small,
/// fully-walled room must read as indoors; a position genuinely clear of
/// every wall must read as outdoors; and the predicate must be a pure
/// function of its inputs.
/// </summary>
public sealed class IndoorPresenceTests
{
    private const string AngleHouseMapText = """
        HKMAP 1
        NAME angle-house
        GRID 640 720 4
        WALL 0 0 0 720 1
        WALL 0 0 640 0 1
        WALL 0 640 300 640 1
        WALL 0 720 640 720 1
        WALL 60 260 200 340 2
        WALL 60 460 60 580 1
        WALL 60 460 100 460 1
        WALL 60 580 180 580 1
        WALL 120 120 320 220 2
        WALL 140 460 180 460 1
        WALL 160 400 340 520 2
        WALL 180 460 180 580 1
        WALL 320 220 520 160 2
        WALL 340 640 640 640 1
        WALL 380 380 560 300 2
        WALL 420 60 420 120 1
        WALL 420 60 600 60 1
        WALL 420 160 420 200 1
        WALL 420 200 600 200 3
        WALL 600 60 600 200 1
        WALL 640 0 640 720 1
        DOOR 100 460 140 460 0 0
        DOOR 300 640 340 640 0 0
        DOOR 420 120 420 160 1 1
        COVER 200 200 260 240 49152 8192 1
        COVER 260 100 340 140 16384 8192 2
        COVER 440 440 520 500 49152 8192 1
        COVER 500 540 560 600 0 32768 1
        SPAWN 0 296 690 49152
        SPAWN 0 320 690 49152
        SPAWN 1 120 520 49152
        SPAWN 1 500 120 16384
        OBJECTIVE 0 500 120 48
        OBJECTIVE 1 120 520 48
        END
        """;

    /// <summary>
    /// Bakes the embedded angle-house geometry into the same
    /// <see cref="NavGrid"/> and <see cref="WallBuckets"/> pair
    /// <c>SandataGame</c> builds from this map at load, following
    /// <c>PathBlockedCellsTests</c>' identical fixture-driven setup in
    /// <c>tests/Sandata.Core.Tests</c>.
    /// </summary>
    private static (NavGrid Grid, WallBuckets WallBuckets) BuildAngleHouse()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(AngleHouseMapText));

        var gridRecord = canonical.OfType<GridRecord>().Single();
        var walls = canonical.OfType<WallRecord>().ToList();
        var doors = canonical.OfType<DoorRecord>().ToList();

        var widthCells = gridRecord.WidthWu / NavGrid.CellSizeWu;
        var heightCells = gridRecord.HeightWu / NavGrid.CellSizeWu;
        var grid = new NavGrid(widthCells, heightCells);

        // Matches SandataGame's own PlaceholderBodyRadiusWu, so the baked
        // passability here is the same shape the shipped client produces
        // from this map.
        const int BodyRadiusWu = 5;
        NavBake.Bake(grid, walls, doors, BodyRadiusWu);

        var segmentAX = new long[walls.Count];
        var segmentAY = new long[walls.Count];
        var segmentBX = new long[walls.Count];
        var segmentBY = new long[walls.Count];
        for (var index = 0; index < walls.Count; index++)
        {
            segmentAX[index] = walls[index].X1;
            segmentAY[index] = walls[index].Y1;
            segmentBX[index] = walls[index].X2;
            segmentBY[index] = walls[index].Y2;
        }

        var wallBuckets = WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);

        return (grid, wallBuckets);
    }

    /// <summary>
    /// Design section 12's blue spawn coordinates, (296, 690) and (320, 690):
    /// both sit in the large bottom room (world y in [640, 720]) that holds
    /// the blue start, well short of any wall on at least the east and west
    /// sides — the room is 640 world units wide, far beyond
    /// <see cref="IndoorPresence.ProbeRangeWu"/> — so neither position can
    /// read as enclosed under this type's eight-direction predicate. This is
    /// the exact regression the task exists to fix: a shooter here must be
    /// able to reach <c>outdoor</c>/<c>distant</c> audio files, not the
    /// <c>indoor</c> ones every prior call site hardcoded.
    /// </summary>
    [Theory]
    [InlineData(296, 690)]
    [InlineData(320, 690)]
    public void IsIndoors_AtEitherBlueSpawn_ReturnsFalse(long positionX, long positionY)
    {
        var (grid, wallBuckets) = BuildAngleHouse();

        Assert.False(IndoorPresence.IsIndoors(positionX, positionY, grid, wallBuckets));
    }

    /// <summary>
    /// (90, 520) sits inside the one small, fully-walled room the map
    /// defines: <c>WALL 60 460 60 580</c> (west), <c>WALL 180 460 180 580</c>
    /// (east), <c>WALL 60 580 180 580</c> (south), and <c>WALL 60 460 100
    /// 460</c> (the western half of the north wall — x = 90 falls inside its
    /// [60, 100] span). The point is offset west of the room's x-centre
    /// (120) deliberately, so it does not sit directly under
    /// <c>DOOR 100 460 140 460 0 0</c>, the closed door occupying the other
    /// half of the north wall: a closed door's footprint is never registered
    /// in <see cref="WallBuckets"/> (only a genuine <c>WALL</c> record is —
    /// see <c>WeaponLoweredRules</c>'s remarks), so a probe cast straight
    /// through the doorway would sail past it, and centring the point under
    /// the door would falsely read as not-indoors on the north probe alone.
    /// Every one of the eight probes from (90, 520) — the nearest wall in
    /// every direction is at most the room's own half-width or half-height,
    /// well inside <see cref="IndoorPresence.ProbeRangeWu"/> — reaches a real
    /// wall, so the room reads as enclosed.
    /// </summary>
    [Fact]
    public void IsIndoors_WellInsideTheOneEnclosedRoom_ReturnsTrue()
    {
        var (grid, wallBuckets) = BuildAngleHouse();

        Assert.True(IndoorPresence.IsIndoors(90, 520, grid, wallBuckets));
    }

    /// <summary>
    /// (300, 100) is genuinely clear of every wall on the map, not merely of
    /// the walls that happen to lie along one of the eight probe directions:
    /// the closest of the twenty-one <c>WALL</c> segments in
    /// <c>angle-house.hkmap</c> to this point (the western end of
    /// <c>WALL 420 60 420 120</c>) is roughly 98 world units away by
    /// perpendicular distance, which already exceeds
    /// <see cref="IndoorPresence.ProbeRangeWu"/> (96) — so no probe of that
    /// length, cast in any direction from this point, can reach a wall at
    /// all. That makes this a stronger negative than the two spawns above,
    /// which do have a nearby wall on some sides; this position has none on
    /// any side.
    /// </summary>
    [Fact]
    public void IsIndoors_FarOutsideEveryWall_ReturnsFalse()
    {
        var (grid, wallBuckets) = BuildAngleHouse();

        Assert.False(IndoorPresence.IsIndoors(300, 100, grid, wallBuckets));
    }

    /// <summary>
    /// The predicate reads only its four inputs and holds no state of its
    /// own, so the same query run twice against the same baked grid and wall
    /// buckets must agree with itself — the determinism bar every
    /// presentation helper this codebase ships is still expected to clear,
    /// even though nothing here reaches the state hash.
    /// </summary>
    [Fact]
    public void IsIndoors_CalledTwiceWithIdenticalInputs_ReturnsTheSameAnswerBothTimes()
    {
        var (grid, wallBuckets) = BuildAngleHouse();

        var first = IndoorPresence.IsIndoors(90, 520, grid, wallBuckets);
        var second = IndoorPresence.IsIndoors(90, 520, grid, wallBuckets);

        Assert.Equal(first, second);
        Assert.True(first);
    }
}
