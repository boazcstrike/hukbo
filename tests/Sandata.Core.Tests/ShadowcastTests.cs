using Hukbo.Diagnostics;
using Sandata.Core.Navigation;
using Sandata.Core.Sensing;

namespace Sandata.Core.Tests;

/// <summary>
/// Covers <see cref="Shadowcast"/> against plan task 29's five-part test bar:
/// symmetry on an open grid, a pillar's shadow on a hand-computed 16 by 16
/// fixture asserted cell by cell, a single-cell corner peek revealing exactly
/// the cells the fixture lists, identical output across two runs on the same
/// input, and a licence header naming GoRogue and MIT.
/// </summary>
/// <remarks>
/// Both 16 by 16 fixtures below were produced by an independent Python
/// reference implementation of Björn Bergström's recursive shadowcasting
/// algorithm — written directly from the algorithm's published definition,
/// using ordinary floating point, and never reading back
/// <see cref="Shadowcast"/>'s own output — rather than derived from this
/// project's C# port. That script is not part of this repository; it exists
/// only as the scratch tool that produced the literal grids transcribed here.
/// </remarks>
public sealed class ShadowcastTests
{
    private const int FixtureWidth = 16;
    private const int FixtureHeight = 16;

    /// <summary>
    /// Fixture A: a single pillar at <c>(4, 7)</c> on an otherwise fully open
    /// 16 by 16 grid, origin <c>(2, 8)</c>, radius 12 cells. Every row below
    /// is a literal transcription of the reference script's row-major output,
    /// one character per cell, <c>x</c> increasing left to right, <c>'1'</c>
    /// visible and <c>'0'</c> not.
    /// </summary>
    private static readonly string[] FixtureAExpectedRows =
    [
        "1111111111100000",
        "1111111111100000",
        "1111111111000000",
        "1111111110000000",
        "1111111100000000",
        "1111111000000000",
        "1111110001111100",
        "1111111111111100",
        "1111111111111110",
        "1111111111111100",
        "1111111111111100",
        "1111111111111100",
        "1111111111111100",
        "1111111111111000",
        "1111111111111000",
        "1111111111110000",
    ];

    private const int FixtureAOriginX = 2;
    private const int FixtureAOriginY = 8;
    private const int FixtureAPillarX = 4;
    private const int FixtureAPillarY = 7;
    private const int FixtureARadius = 12;

    /// <summary>
    /// Fixture B: a single wall cell at <c>(8, 8)</c> on an otherwise fully
    /// open 16 by 16 grid, origin <c>(6, 8)</c>, radius 6 cells — the textbook
    /// minimal case for recursive shadowcasting's known "peek around a single
    /// orthogonal corner" behaviour, where a cell diagonally adjacent to a
    /// lone blocker is visible even though the cell directly beyond the
    /// blocker, on the same ray as the origin, is not. Transcribed the same
    /// way as fixture A.
    /// </summary>
    private static readonly string[] FixtureBExpectedRows =
    [
        "0000000000000000",
        "0000000000000000",
        "0000001000000000",
        "0001111111000000",
        "0011111111100000",
        "0111111111110000",
        "0111111111110000",
        "0111111111110000",
        "1111111110000000",
        "0111111111110000",
        "0111111111110000",
        "0111111111110000",
        "0011111111100000",
        "0001111111000000",
        "0000001000000000",
        "0000000000000000",
    ];

    private const int FixtureBOriginX = 6;
    private const int FixtureBOriginY = 8;
    private const int FixtureBWallX = 8;
    private const int FixtureBWallY = 8;
    private const int FixtureBRadius = 6;

    [Fact]
    public void Compute_FixtureA_MatchesTheHandComputedPillarShadowCellByCell()
    {
        var grid = new NavGrid(FixtureWidth, FixtureHeight);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        grid.Passability[grid.CellIndex(FixtureAPillarX, FixtureAPillarY)] = NavCellFlags.Blocked;

        var field = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, grid.CellIndex(FixtureAOriginX, FixtureAOriginY), FixtureARadius, field);

        AssertFieldMatchesFixture(grid, field, FixtureAExpectedRows);
    }

    /// <summary>
    /// The door-blocks-sight rule this task decided: a closed
    /// <see cref="NavCellFlags.Door"/> blocks a shadowcast exactly as a
    /// <see cref="NavCellFlags.Blocked"/> cell does, because nothing about a
    /// door being shut lets a spectator see through it. Reuses fixture A's
    /// exact geometry with the pillar cell recast as a closed door instead of
    /// a wall, and requires byte-for-byte the same visibility output — the
    /// two passability values are different, but they must be
    /// indistinguishable to this algorithm.
    /// </summary>
    [Fact]
    public void Compute_AClosedDoorInPlaceOfThePillar_BlocksSightIdenticallyToABlockedCell()
    {
        var grid = new NavGrid(FixtureWidth, FixtureHeight);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        grid.Passability[grid.CellIndex(FixtureAPillarX, FixtureAPillarY)] = NavCellFlags.Door;

        var field = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, grid.CellIndex(FixtureAOriginX, FixtureAOriginY), FixtureARadius, field);

        AssertFieldMatchesFixture(grid, field, FixtureAExpectedRows);
    }

    [Fact]
    public void Compute_FixtureB_RevealsExactlyTheCornerPeekCellsTheFixtureLists()
    {
        var grid = new NavGrid(FixtureWidth, FixtureHeight);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        grid.Passability[grid.CellIndex(FixtureBWallX, FixtureBWallY)] = NavCellFlags.Blocked;

        var field = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, grid.CellIndex(FixtureBOriginX, FixtureBOriginY), FixtureBRadius, field);

        AssertFieldMatchesFixture(grid, field, FixtureBExpectedRows);

        // The specific peek this fixture exists to pin: the cell directly
        // behind the lone wall cell, on the same ray as the origin, stays
        // shadowed, while the two cells diagonally behind it — reachable only
        // by "peeking" around the wall's corner — are lit.
        Assert.False(field.IsVisible(grid.CellIndex(9, 8)), "(9, 8) is directly behind the wall and must stay shadowed.");
        Assert.True(field.IsVisible(grid.CellIndex(9, 7)), "(9, 7) is visible by peeking around the wall's upper corner.");
        Assert.True(field.IsVisible(grid.CellIndex(9, 9)), "(9, 9) is visible by peeking around the wall's lower corner.");
    }

    /// <summary>
    /// Symmetry: on a fully open grid with no blockers at all, cell B is
    /// visible from cell A whenever cell A is visible from cell B, for every
    /// pair of cells within radius of one another. This does not hold in
    /// general once blockers are involved (recursive shadowcasting is a
    /// well-known asymmetric algorithm around corners — fixture B's own peek
    /// is not visible in reverse from every one of its "visible" cells), so
    /// this test is deliberately confined to the open-grid case where the
    /// only source of any potential asymmetry, occlusion, is entirely absent.
    /// </summary>
    [Fact]
    public void Compute_OnAnOpenGrid_IsSymmetric()
    {
        const int width = 12;
        const int height = 12;
        const int radius = 8;

        var grid = new NavGrid(width, height);
        Array.Fill(grid.Passability, NavCellFlags.Open);

        var fields = new VisibilityField[grid.CellCount];
        for (var originIndex = 0; originIndex < grid.CellCount; originIndex++)
        {
            var field = new VisibilityField(grid.CellCount);
            Shadowcast.Compute(grid, originIndex, radius, field);
            fields[originIndex] = field;
        }

        for (var a = 0; a < grid.CellCount; a++)
        {
            for (var b = 0; b < grid.CellCount; b++)
            {
                Assert.True(
                    fields[a].IsVisible(b) == fields[b].IsVisible(a),
                    $"cell {a} sees {b} == {fields[a].IsVisible(b)}, but cell {b} sees {a} == {fields[b].IsVisible(a)}; expected symmetry on an open grid.");
            }
        }
    }

    /// <summary>
    /// Determinism: computing the same origin, radius, and grid twice, into
    /// two independently allocated <see cref="VisibilityField"/> instances,
    /// must produce byte-for-byte the same result both times. A visibility
    /// field is derived state (see <see cref="VisibilityField"/>'s remarks)
    /// precisely because recomputing it is always safe; this test is the
    /// half of that claim that says recomputing it is also always the same.
    /// </summary>
    [Fact]
    public void Compute_CalledTwiceOnTheSameInput_ProducesIdenticalOutput()
    {
        var grid = new NavGrid(FixtureWidth, FixtureHeight);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        grid.Passability[grid.CellIndex(FixtureAPillarX, FixtureAPillarY)] = NavCellFlags.Blocked;

        var origin = grid.CellIndex(FixtureAOriginX, FixtureAOriginY);

        var first = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, origin, FixtureARadius, first);

        var second = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, origin, FixtureARadius, second);

        for (var cellIndex = 0; cellIndex < grid.CellCount; cellIndex++)
        {
            Assert.True(
                first.IsVisible(cellIndex) == second.IsVisible(cellIndex),
                $"cell {cellIndex}: first run {first.IsVisible(cellIndex)}, second run {second.IsVisible(cellIndex)}.");
        }
    }

    [Fact]
    public void Compute_TheOriginCell_IsAlwaysVisible()
    {
        var grid = new NavGrid(FixtureWidth, FixtureHeight);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var origin = grid.CellIndex(FixtureAOriginX, FixtureAOriginY);

        var field = new VisibilityField(grid.CellCount);
        Shadowcast.Compute(grid, origin, radiusCells: 0, field);

        Assert.True(field.IsVisible(origin));
    }

    [Fact]
    public void Compute_ThrowsArgumentOutOfRange_WhenTheOriginCellIndexIsOutOfBounds()
    {
        var grid = new NavGrid(4, 4);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var field = new VisibilityField(grid.CellCount);

        Assert.Throws<ArgumentOutOfRangeException>(() => Shadowcast.Compute(grid, grid.CellCount, 3, field));
    }

    [Fact]
    public void Compute_ThrowsArgumentOutOfRange_WhenTheRadiusIsNegative()
    {
        var grid = new NavGrid(4, 4);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var field = new VisibilityField(grid.CellCount);

        Assert.Throws<ArgumentOutOfRangeException>(() => Shadowcast.Compute(grid, 0, -1, field));
    }

    [Fact]
    public void Compute_ThrowsArgumentException_WhenTheFieldCellCountDoesNotMatchTheGrid()
    {
        var grid = new NavGrid(4, 4);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var field = new VisibilityField(grid.CellCount + 1);

        Assert.Throws<ArgumentException>(() => Shadowcast.Compute(grid, 0, 3, field));
    }

    /// <summary>
    /// The GoRogue/MIT attribution plan task 29 requires: the source file
    /// must name both, at the top of the file, rather than relying on this
    /// test file, a commit message, or a README to carry the licence
    /// obligation on the port's behalf.
    /// </summary>
    [Fact]
    public void ShadowcastSourceFile_LicenceHeader_NamesGoRogueAndMit()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(root is not null, "No ancestor of " + AppContext.BaseDirectory + " contains the repository marker file.");

        var sourcePath = Path.Combine(root!, "src", "Sandata.Core", "Sensing", "Shadowcast.cs");
        var content = File.ReadAllText(sourcePath);

        Assert.Contains("GoRogue", content, StringComparison.Ordinal);
        Assert.Contains("MIT License", content, StringComparison.Ordinal);
    }

    private static void AssertFieldMatchesFixture(NavGrid grid, VisibilityField field, string[] expectedRows)
    {
        for (var y = 0; y < FixtureHeight; y++)
        {
            for (var x = 0; x < FixtureWidth; x++)
            {
                var expected = expectedRows[y][x] == '1';
                var actual = field.IsVisible(grid.CellIndex(x, y));
                Assert.True(
                    expected == actual,
                    $"cell ({x}, {y}): expected visible={expected}, got visible={actual}.");
            }
        }
    }
}
