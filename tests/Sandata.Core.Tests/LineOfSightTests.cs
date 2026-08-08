using Sandata.Core.Geometry;
using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Proves the two-phase shape design section 7 requires: the broad phase
/// (<see cref="GridRay.Traverse"/> via <see cref="WallBuckets"/>) only ever
/// proposes candidate walls, and the narrow phase
/// (<see cref="ExactPredicates.ClassifySegments"/>) is what actually decides
/// occlusion — including the case where the two disagree.
/// </summary>
public sealed class LineOfSightTests
{
    // Shared by every fixture below: large enough to hold every coordinate
    // any test in this file uses, with room to spare.
    private static NavGrid NewGrid() => new(width: 8, height: 8);

    [Fact]
    public void SupercoverAndExactPredicateDisagree_AndTheExactPredicateWins()
    {
        // The wall is design section 7's own example angle: dy/dx = 4/12 =
        // 1/3, atan(1/3) ~= 18.43 degrees. Its supercover, as walked cell by
        // cell, is (0,0), (1,0), (2,0), (2,1), (3,1) — it passes near the
        // BOTTOM of row 0's cells, climbing from y=1 to y=2 over x=0..4.
        var grid = NewGrid();
        var wallAX = new long[] { 0 };
        var wallAY = new long[] { 1 };
        var wallBX = new long[] { 12 };
        var wallBY = new long[] { 5 };
        var wallBuckets = WallBuckets.Build(grid, wallAX, wallAY, wallBX, wallBY);

        // The query is a horizontal line along the TOP of the same two
        // cells the wall's supercover touches: (0, 3) to (4, 3). Both
        // segments pass through cells (0, 0) and (1, 0), so a check that
        // stopped at "do they share a cell" would call this blocked.
        var sharedCells = new[] { grid.CellIndex(0, 0), grid.CellIndex(1, 0) };
        Span<int> queryCells = new int[8];
        var queryCellCount = GridRay.Traverse(originX: 0, originY: 3, targetX: 4, targetY: 3, queryCells, grid);
        foreach (var sharedCell in sharedCells)
        {
            Assert.Contains(sharedCell, queryCells[..queryCellCount].ToArray());
        }

        // The wall's own bucket build also visited both cells, so the
        // candidate is genuinely present for the narrow phase to consider —
        // this is not a test that passes only because the candidate list is
        // empty.
        Assert.Contains(0, wallBuckets.SegmentsInCell(grid.CellIndex(0, 0)).ToArray());
        Assert.Contains(0, wallBuckets.SegmentsInCell(grid.CellIndex(1, 0)).ToArray());

        // The exact predicate is what actually decides it: at x in [0, 4]
        // the wall's true line sits between y = 1 and y = 2, nowhere near
        // the query's y = 3, so the two segments never cross.
        var relation = ExactPredicates.ClassifySegments(0, 3, 4, 3, 0, 1, 12, 5);
        Assert.Equal(SegmentRelation.Disjoint, relation);

        // LineOfSight must agree with the exact predicate, not the
        // supercover cell membership it disagrees with.
        Assert.True(LineOfSight.IsVisible(originX: 0, originY: 3, targetX: 4, targetY: 3, grid, wallBuckets));
        Assert.Equal(-1, LineOfSight.FirstBlockingSegment(originX: 0, originY: 3, targetX: 4, targetY: 3, grid, wallBuckets));
    }

    [Fact]
    public void WallActuallyCrossingTheSightline_IsReportedAsBlocking()
    {
        // A straightforward positive control: a vertical wall crossing a
        // horizontal query line at a right angle, so there is no ambiguity
        // about whether the two segments actually meet.
        var grid = NewGrid();
        var wallAX = new long[] { 5 };
        var wallAY = new long[] { 0 };
        var wallBX = new long[] { 5 };
        var wallBY = new long[] { 8 };
        var wallBuckets = WallBuckets.Build(grid, wallAX, wallAY, wallBX, wallBY);

        Assert.False(LineOfSight.IsVisible(originX: 0, originY: 4, targetX: 10, targetY: 4, grid, wallBuckets));
        Assert.Equal(0, LineOfSight.FirstBlockingSegment(originX: 0, originY: 4, targetX: 10, targetY: 4, grid, wallBuckets));
    }

    [Fact]
    public void TwoBlockingWallsInTheSameCell_ReportsTheLowerRecordIndex()
    {
        // Both walls are vertical lines inside the single cell the query
        // never leaves (query spans x in [0, 3], all inside cell (0, 0)),
        // so cell-visitation order cannot be what decides which is
        // reported — only the ascending record index WallBuckets.Build
        // inserts in can. Record index 0 is the FARTHER wall (x = 2) and
        // record index 1 is the NEARER one (x = 1), specifically so this
        // test cannot pass by coincidence if the implementation quietly
        // picked "nearest along the ray" instead of "lowest record index".
        var grid = NewGrid();
        var wallAX = new long[] { 2, 1 };
        var wallAY = new long[] { 0, 0 };
        var wallBX = new long[] { 2, 1 };
        var wallBY = new long[] { 4, 4 };
        var wallBuckets = WallBuckets.Build(grid, wallAX, wallAY, wallBX, wallBY);

        var blocking = LineOfSight.FirstBlockingSegment(originX: 0, originY: 2, targetX: 3, targetY: 2, grid, wallBuckets);

        Assert.Equal(0, blocking);
    }

    [Fact]
    public void NoWallsAtAll_IsVisible()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        Assert.True(LineOfSight.IsVisible(originX: 0, originY: 0, targetX: 20, targetY: 20, grid, wallBuckets));
        Assert.Equal(-1, LineOfSight.FirstBlockingSegment(originX: 0, originY: 0, targetX: 20, targetY: 20, grid, wallBuckets));
    }
}
