using System.IO;
using System.Linq;
using Sandata.Core.Maps;

namespace Sandata.Core.Tests;

/// <summary>
/// The golden-fixture test bar for task 25: <c>angle-house.hkmap</c>
/// (docs/plans/2026-08-07-sandata-scaffold-design.md section 12, reproduced
/// verbatim at tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap) parses,
/// canonicalises to itself byte for byte, passes every
/// <see cref="MapValidator"/> cross-record rule, and carries the exact
/// counts of non-orthogonal walls, door states, 360-degree cover, and the
/// one interior material-3 wall that design section 12 calls for. The
/// pinned <see cref="MapContentHash"/> value below was measured from a real
/// run of this test, never calculated by hand.
/// </summary>
public sealed class AngleHouseFixtureTests
{
    private static string LoadFixtureText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");

        Assert.True(
            File.Exists(path),
            $"The angle-house fixture is missing at '{path}'. It is committed under " +
            "tests/Sandata.Core.Tests/Fixtures and copied to the output directory by " +
            "the project's Fixtures item.");

        return File.ReadAllText(path);
    }

    [Fact]
    public void FixtureParsesWithoutThrowing()
    {
        MapTokenizer.Tokenize(LoadFixtureText());
    }

    /// <summary>
    /// Design section 12 prints <c>angle-house.hkmap</c> already in
    /// canonical order — every WALL, DOOR, COVER, SPAWN, and OBJECTIVE line
    /// sorted by (kindOrdinal, fields...) with every WALL/DOOR endpoint
    /// already lexicographic-ascending. Canonicalising the parsed records
    /// must therefore produce the exact same byte stream as encoding the
    /// raw tokenized order directly.
    /// </summary>
    [Fact]
    public void FixtureCanonicalisesToItselfByteForByte()
    {
        var tokenized = MapTokenizer.Tokenize(LoadFixtureText());
        var canonical = MapCanonicalizer.Canonicalize(tokenized);

        Assert.Equal(
            MapContentHash.Encode(tokenized).ToArray(),
            MapContentHash.Encode(canonical).ToArray());
    }

    [Fact]
    public void FixturePassesEveryMapValidatorCrossRecordRule()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));

        MapValidator.Validate(canonical);
    }

    [Fact]
    public void FixtureHasExactlyFiveWallsNeitherHorizontalNorVertical()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));
        var walls = canonical.OfType<WallRecord>();

        var nonOrthogonalCount = walls.Count(w => w.X1 != w.X2 && w.Y1 != w.Y2);

        Assert.Equal(5, nonOrthogonalCount);
    }

    [Fact]
    public void FixtureHasOneOpenAndTwoClosedDoors()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));
        var doors = canonical.OfType<DoorRecord>().ToArray();

        Assert.Equal(3, doors.Length);
        Assert.Equal(1, doors.Count(d => d.State == 1));
        Assert.Equal(2, doors.Count(d => d.State == 0));
    }

    [Fact]
    public void FixtureHasExactlyOne360DegreeCover()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));
        var covers = canonical.OfType<CoverRecord>();

        Assert.Equal(1, covers.Count(c => c.ArcHalfBam == 32768));
    }

    /// <summary>
    /// Exactly one <see cref="WallRecord"/> has <see cref="WallRecord.Material"/>
    /// 3 (breachable), and its endpoints touch none of the map's four
    /// boundary lines (x = 0, x = 640, y = 0, y = 720) — it is an interior
    /// wall, not part of the outer shell.
    /// </summary>
    [Fact]
    public void FixtureHasOneMaterial3WallNotOnTheOuterShell()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));
        var grid = canonical.OfType<GridRecord>().Single();
        var material3Walls = canonical.OfType<WallRecord>().Where(w => w.Material == 3).ToArray();

        Assert.Single(material3Walls);

        var wall = material3Walls[0];
        Assert.NotEqual(0, wall.X1);
        Assert.NotEqual(0, wall.X2);
        Assert.NotEqual(grid.WidthWu, wall.X1);
        Assert.NotEqual(grid.WidthWu, wall.X2);
        Assert.NotEqual(0, wall.Y1);
        Assert.NotEqual(0, wall.Y2);
        Assert.NotEqual(grid.HeightWu, wall.Y1);
        Assert.NotEqual(grid.HeightWu, wall.Y2);
    }

    /// <summary>
    /// The recorded expectation for angle-house.hkmap's content hash,
    /// computed from the built code by running this test, never calculated
    /// by hand. If a future edit to the fixture, to
    /// <see cref="MapCanonicalizer"/>'s field order, or to the FNV-1a fold
    /// moves this value, that is a deliberate content change with a new
    /// recorded expectation, not a fix to this test.
    /// </summary>
    [Fact]
    public void FixtureContentHashIsPinned()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));

        Assert.Equal(11909359227906322716UL, MapContentHash.Compute(canonical));
    }
}
