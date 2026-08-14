using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Plan doc 2026-08-14-sandata-lowered-weapon-and-automatic-fire, design
/// decision D1's defect and fix: <see cref="SandataSimulation"/> used to
/// allocate <c>_pathBlockedCells</c> and never write it, so stage 7's search
/// read an all-<see langword="false"/> blocked span and ignored every wall
/// and closed door on the map. <see cref="SandataSimulation.BuildPathBlockedCells"/>
/// is the fix — a static seed read once, at construction, from the baked
/// <see cref="NavGrid.Passability"/> — and this file proves it three ways:
/// a hand-built fixture whose straight-line route crosses a wall, the real
/// <c>angle-house.hkmap</c> fixture whose only route from the blue spawns to
/// the first objective crosses a closed door, and a wall-free grid whose
/// seeded blocked span must still match the exact all-false content the
/// pre-fix code always produced.
/// </summary>
public sealed class PathBlockedCellsTests
{
    /// <summary>
    /// A single wall column, baked with a small body radius so its
    /// inflation narrows the crossing without closing it. The straight
    /// row-0 line from start to goal crosses the column directly; a search
    /// fed <see cref="SandataSimulation.BuildPathBlockedCells"/>'s seed must
    /// detour around it instead, never entering a
    /// <see cref="NavCellFlags.Blocked"/> cell, and in particular never
    /// entering cell (3, 0), the cell the pre-fix all-false blocked span
    /// would have walked straight through.
    /// </summary>
    [Fact]
    public void TryFindPath_WithTheSeededBlockedSpan_DetoursAroundAWallItWouldOtherwiseWalkThrough()
    {
        var grid = new NavGrid(width: 6, height: 6);

        // Vertical wall at world x = 12 (cell x = 3), spanning world y in
        // [0, 8] -- supercover cells (3, 0), (3, 1), (3, 2). Body radius 1 wu
        // (inflateCells == ceil(1 / 4) == 1 cell) widens the blocked band to
        // roughly x in [2, 4], y in [0, 3], leaving rows 4 and 5 open all the
        // way across the grid as the only crossing.
        var wall = new WallRecord(LineNumber: 1, X1: 12, Y1: 0, X2: 12, Y2: 8, Material: 1);
        NavBake.Bake(grid, walls: [wall], doors: [], bodyRadiusWu: 1);

        var blocked = SandataSimulation.BuildPathBlockedCells(grid);

        var start = grid.CellIndex(0, 0);
        var goal = grid.CellIndex(5, 0);

        var search = new NavSearch();
        var path = new List<int>();
        var expanded = new List<int>();
        var outcome = search.TryFindPath(grid, start, goal, blocked, path, expanded);

        Assert.Equal(NavSearchOutcome.PathFound, outcome);
        Assert.NotEmpty(path);

        foreach (var cellIndex in path)
        {
            Assert.NotEqual(NavCellFlags.Blocked, grid.Passability[cellIndex]);
        }

        // The naive straight-line route along row 0 would pass through cell
        // (3, 0), which the wall's own rasterization blocks outright (before
        // inflation even applies). Its absence here is what proves the
        // detour actually happened, not merely that the algorithm never
        // returns a blocked cell by construction.
        Assert.DoesNotContain(grid.CellIndex(3, 0), path);

        // A genuine detour down to row 4 or 5 and back costs strictly more
        // steps than the six-cell straight line across row 0 would have.
        Assert.True(
            path.Count > 6,
            $"Expected a detour longer than the direct 6-cell row, but the path had {path.Count} cells.");
    }

    /// <summary>
    /// The regression guard: on a grid with no walls and no doors,
    /// <see cref="NavBake.Bake"/> leaves every cell
    /// <see cref="NavCellFlags.Open"/>, so
    /// <see cref="SandataSimulation.BuildPathBlockedCells"/>'s seed must come
    /// out byte-for-byte identical to a freshly allocated <c>bool[]</c> --
    /// exactly the array the pre-fix constructor left in place forever — and
    /// a search fed either array must return the identical outcome, path,
    /// and expanded-node sequence. This is what proves the fix changes
    /// behaviour only on a map that actually has walls or closed doors.
    /// </summary>
    [Fact]
    public void BuildPathBlockedCells_OnAWallFreeGrid_MatchesTheAllFalseSpanFromBeforeTheFix()
    {
        var grid = new NavGrid(width: 5, height: 5);
        NavBake.Bake(grid, walls: [], doors: [], bodyRadiusWu: 5);

        var seeded = SandataSimulation.BuildPathBlockedCells(grid);
        var preFixAllFalse = new bool[grid.CellCount];

        Assert.Equal(preFixAllFalse, seeded);

        var start = grid.CellIndex(0, 0);
        var goal = grid.CellIndex(4, 4);

        var seededSearch = new NavSearch();
        var seededPath = new List<int>();
        var seededExpanded = new List<int>();
        var seededOutcome = seededSearch.TryFindPath(grid, start, goal, seeded, seededPath, seededExpanded);

        var preFixSearch = new NavSearch();
        var preFixPath = new List<int>();
        var preFixExpanded = new List<int>();
        var preFixOutcome = preFixSearch.TryFindPath(grid, start, goal, preFixAllFalse, preFixPath, preFixExpanded);

        Assert.Equal(NavSearchOutcome.PathFound, seededOutcome);
        Assert.Equal(preFixOutcome, seededOutcome);
        Assert.Equal(preFixPath, seededPath);
        Assert.Equal(preFixExpanded, seededExpanded);
    }

    /// <summary>
    /// The real fixture. The bottom room holding both blue spawns is closed
    /// off from the rest of the house by a wall spanning the map's full
    /// width at world y = 640, with exactly one gap: <c>DOOR 300 640 340 640
    /// 0 0</c>, closed. That is the only way across for any search that
    /// honours the map's real passability, so a search fed
    /// <see cref="SandataSimulation.BuildPathBlockedCells"/>'s seed must
    /// reach the first objective through that door's surviving
    /// <see cref="NavCellFlags.Door"/> cells, and must never cross the wall
    /// itself.
    /// </summary>
    [Fact]
    public void TryFindPath_ThroughTheAngleHouseFixture_CrossesTheClosedDoorApertureNotTheWall()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));

        var gridRecord = canonical.OfType<GridRecord>().Single();
        var walls = canonical.OfType<WallRecord>().ToList();
        var doors = canonical.OfType<DoorRecord>().ToList();

        var widthCells = gridRecord.WidthWu / NavGrid.CellSizeWu;
        var heightCells = gridRecord.HeightWu / NavGrid.CellSizeWu;
        var grid = new NavGrid(widthCells, heightCells);

        // Matches SandataGame's own PlaceholderBodyRadiusWu, so this
        // fixture's baked passability is the same shape the shipped client
        // would produce from the identical map and wall/door data.
        const int BodyRadiusWu = 5;
        NavBake.Bake(grid, walls, doors, BodyRadiusWu);

        var blocked = SandataSimulation.BuildPathBlockedCells(grid);

        // Design section 12's own blue spawn coordinates: (296, 690) and
        // (320, 690), both in the bottom room. Only the first is the
        // search's start; both exist on the map regardless.
        var start = grid.CellIndex(
            NavGrid.WorldToCellCoordinate(296), NavGrid.WorldToCellCoordinate(690));
        var goal = grid.CellIndex(
            NavGrid.WorldToCellCoordinate(500), NavGrid.WorldToCellCoordinate(120));

        var search = new NavSearch();
        var path = new List<int>();
        var expanded = new List<int>();
        var outcome = search.TryFindPath(grid, start, goal, blocked, path, expanded);

        Assert.Equal(NavSearchOutcome.PathFound, outcome);

        foreach (var cellIndex in path)
        {
            Assert.NotEqual(NavCellFlags.Blocked, grid.Passability[cellIndex]);
        }

        // The row of cells the y = 640 wall (and its one door gap) baked to.
        var wallRowCellY = NavGrid.WorldToCellCoordinate(640);
        var doorApertureCells = new HashSet<int>();
        var wallCellsOnThatRow = new List<int>();

        for (var cellX = 0; cellX < grid.Width; cellX++)
        {
            var cellIndex = grid.CellIndex(cellX, wallRowCellY);

            if (grid.Passability[cellIndex] == NavCellFlags.Door)
            {
                doorApertureCells.Add(cellIndex);
            }
            else if (grid.Passability[cellIndex] == NavCellFlags.Blocked)
            {
                wallCellsOnThatRow.Add(cellIndex);
            }
        }

        Assert.NotEmpty(doorApertureCells);
        Assert.NotEmpty(wallCellsOnThatRow);

        Assert.Contains(path, cellIndex => doorApertureCells.Contains(cellIndex));

        foreach (var wallCellIndex in wallCellsOnThatRow)
        {
            Assert.DoesNotContain(wallCellIndex, path);
        }
    }

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
}
