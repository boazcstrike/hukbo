using System.IO;
using System.Linq;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Stage 0's room-derivation test bar (design decision 1 of
/// <c>docs/plans/2026-08-14-sandata-clear-the-map-design.md</c>): baking
/// <see cref="RoomLayout"/> over the real <c>angle-house.hkmap</c> fixture
/// produces a stable, non-trivial set of room ids, and baking the same
/// fixture twice — a fresh <see cref="NavGrid"/> and fresh wall/door arrays
/// each time — produces byte-identical results, proving <see cref="RoomLayout"/>
/// is a pure function of the baked geometry with no hidden mutable state.
/// </summary>
public sealed class RoomLayoutTests
{
    private static string LoadAngleHouseFixtureText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");

        Assert.True(
            File.Exists(path),
            $"The angle-house fixture is missing at '{path}'. It is committed under " +
            "tests/Sandata.Core.Tests/Fixtures and copied to the output directory by " +
            "the project's Fixtures item.");

        return File.ReadAllText(path);
    }

    private static RoomLayout BakeAngleHouse()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadAngleHouseFixtureText()));
        var gridRecord = canonical.OfType<GridRecord>().Single();
        var grid = new NavGrid(gridRecord.WidthWu / gridRecord.CellWu, gridRecord.HeightWu / gridRecord.CellWu);
        var walls = canonical.OfType<WallRecord>().ToArray();
        var doors = canonical.OfType<DoorRecord>().ToArray();

        return RoomLayout.Bake(grid, walls, doors);
    }

    /// <summary>
    /// angle-house.hkmap has interior walls and doors (see
    /// <see cref="AngleHouseFixtureTests"/>), so a real bake must find more
    /// than one room, and every room id must be a distinct, non-negative
    /// <see cref="NavGrid"/> cell index in strictly ascending order — the
    /// contract <see cref="RoomLayout.RoomIds"/>'s own doc comment states.
    /// </summary>
    [Fact]
    public void BakingTheRealAngleHouseFixture_ProducesAStableNonTrivialRoomSet()
    {
        var layout = BakeAngleHouse();
        var roomIds = layout.RoomIds;

        Assert.True(roomIds.Length > 1, $"expected more than one room, found {roomIds.Length}");

        for (var i = 0; i < roomIds.Length; i++)
        {
            Assert.True(roomIds[i] >= 0);
            if (i > 0)
            {
                Assert.True(roomIds[i] > roomIds[i - 1], "RoomIds must be strictly ascending.");
            }
        }

        // Every RoomId is itself a valid, non-boundary cell of its own room —
        // RoomLayout's own remarks: a room's id is always its own seed cell.
        foreach (var roomId in roomIds)
        {
            Assert.Equal(roomId, layout.RoomIdAt(roomId));
        }
    }

    /// <summary>
    /// Two independent bakes of the same fixture — fresh <see cref="NavGrid"/>,
    /// fresh wall/door arrays each time, no shared instance between them —
    /// produce the exact same <see cref="RoomLayout.RoomIds"/> sequence and
    /// the exact same per-cell room assignment. <see cref="RoomLayout"/>'s own
    /// summary states it is "never authored, never snapshotted... rebuilt
    /// identically on every bake"; this is the test that proves it rather
    /// than merely asserting it in a doc comment.
    /// </summary>
    [Fact]
    public void BakingTheSameFixtureTwice_ProducesIdenticalRoomLayouts()
    {
        var first = BakeAngleHouse();
        var second = BakeAngleHouse();

        // ImmutableArray<T>.Equals(ImmutableArray<T>) compares the two
        // wrapped array references, not element-by-element, so Assert.Equal
        // on the ImmutableArray values themselves would report the two
        // *instances* as unequal even when every element matches. Comparing
        // plain arrays instead forces a real sequence comparison.
        Assert.Equal(first.RoomIds.ToArray(), second.RoomIds.ToArray());

        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadAngleHouseFixtureText()));
        var gridRecord = canonical.OfType<GridRecord>().Single();
        var cellCount = (gridRecord.WidthWu / gridRecord.CellWu) * (gridRecord.HeightWu / gridRecord.CellWu);

        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            Assert.Equal(first.RoomIdAt(cellIndex), second.RoomIdAt(cellIndex));
        }
    }
}
