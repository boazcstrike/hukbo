using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Plan task 67's restated acceptance bar (Sandata's scaffold plan,
/// "The funnel does not deliver the straight line, and task 67 does"): across
/// open ground the smoothed path is exactly two points and equals the taut
/// straight line, asserted as literal coordinates rather than as a
/// vertex-count improvement over the corridor; a path forced around exactly
/// one wall publishes exactly three points; every emitted segment passes
/// <see cref="LineOfSight.IsVisible"/>; and no emitted point can be removed
/// without breaking visibility.
/// </summary>
public sealed class PathSmoothingTests
{
    /// <summary>
    /// Design section 7's own amendment fixture, reproduced exactly: a fully
    /// open ten-by-four cell region with no walls at all, from cell
    /// <c>(0,0)</c> to cell <c>(9,3)</c>. The amendment records the taut path
    /// across this ground as the single segment <c>(2,2)</c> to <c>(38,14)</c>
    /// in world units — the same cell-centre formula <c>Funnel</c> uses,
    /// <c>cellCoordinate * NavGrid.CellSizeWu + CellSizeWu / 2</c>. With no
    /// wall segments to block anything, every corridor point is visible from
    /// the anchor, so the very first scan lands on the goal immediately: the
    /// published path is exactly the two endpoints, not a vertex-count
    /// improvement over whatever corridor A* happened to find.
    /// </summary>
    [Fact]
    public void OpenGround_PublishesExactlyTwoPoints_EqualToTheStraightLine()
    {
        var grid = new NavGrid(width: 10, height: 4);
        var blocked = new bool[grid.CellCount];
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        var start = grid.CellIndex(0, 0);
        var goal = grid.CellIndex(9, 3);

        var pathCellIndices = new List<int>();
        var outcome = new NavSearch().TryFindPath(grid, start, goal, blocked, pathCellIndices, []);
        Assert.Equal(NavSearchOutcome.PathFound, outcome);

        Span<long> outputX = new long[pathCellIndices.Count];
        Span<long> outputY = new long[pathCellIndices.Count];
        var count = PathSmoothing.Smooth(pathCellIndices.ToArray(), outputX, outputY, grid, wallBuckets);

        Assert.Equal(2, count);
        Assert.Equal(2, outputX[0]);
        Assert.Equal(2, outputY[0]);
        Assert.Equal(38, outputX[1]);
        Assert.Equal(14, outputY[1]);

        AssertEverySegmentIsVisible(outputX[..count], outputY[..count], grid, wallBuckets);
    }

    /// <summary>
    /// The same L-shaped corridor <c>FunnelTests.LShapedCorridor_CollapsesToThreePoints</c>
    /// pins, this time with a real solid obstacle occupying the corner cell
    /// the corridor bends around — cell <c>(0,1)</c>'s footprint, world
    /// <c>[0,4) x [4,8)</c> — represented as two wall segments forming that
    /// cell's north and east faces. Those two faces block a straight line
    /// from the corridor's start centre to any of its three furthest cell
    /// centres (the goal among them), because such a line would have to cut
    /// through the corner the obstacle occupies; they do not block the
    /// corridor's own first edge (start centre to the next cell centre,
    /// a straight horizontal line that never reaches the obstacle's row) or
    /// a straight vertical run down the corridor's second column to the goal.
    /// The published path is therefore exactly three points: the start
    /// centre, one elbow, and the goal centre.
    /// </summary>
    [Fact]
    public void PathAroundOneWall_PublishesExactlyThreePoints()
    {
        var grid = new NavGrid(width: 2, height: 4);
        var corridor = new[]
        {
            grid.CellIndex(0, 0),
            grid.CellIndex(1, 0),
            grid.CellIndex(1, 1),
            grid.CellIndex(1, 2),
            grid.CellIndex(1, 3),
        };

        // The corner obstacle's north face, (0,4)-(4,4), and east face,
        // (4,4)-(4,8) — cell (0,1)'s footprint, which the corridor never
        // enters but a cut-corner sightline from the start would have to
        // cross.
        var wallBuckets = WallBuckets.Build(
            grid,
            segmentAX: [0, 4],
            segmentAY: [4, 4],
            segmentBX: [4, 4],
            segmentBY: [4, 8]);

        Span<long> outputX = new long[corridor.Length];
        Span<long> outputY = new long[corridor.Length];
        var count = PathSmoothing.Smooth(corridor, outputX, outputY, grid, wallBuckets);

        Assert.Equal(3, count);

        // The endpoints are the corridor's own start and goal cell centres,
        // regardless of which corridor cell the middle point resolves to.
        Assert.Equal((2L, 2L), (outputX[0], outputY[0]));
        Assert.Equal((6L, 14L), (outputX[2], outputY[2]));

        AssertEverySegmentIsVisible(outputX[..count], outputY[..count], grid, wallBuckets);

        // Minimum vertices: the middle point cannot be removed, because the
        // start and goal centres alone are not mutually visible — exactly
        // the cut-corner line the obstacle exists to block.
        Assert.False(LineOfSight.IsVisible(outputX[0], outputY[0], outputX[2], outputY[2], grid, wallBuckets));
    }

    [Fact]
    public void SingleCellCorridor_PublishesThatOneCentreAlone()
    {
        var grid = new NavGrid(width: 3, height: 3);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var corridor = new[] { grid.CellIndex(1, 1) };

        Span<long> outputX = new long[1];
        Span<long> outputY = new long[1];
        var count = PathSmoothing.Smooth(corridor, outputX, outputY, grid, wallBuckets);

        Assert.Equal(1, count);
        Assert.Equal(6, outputX[0]);
        Assert.Equal(6, outputY[0]);
    }

    [Fact]
    public void EmptyCorridor_Throws()
    {
        var grid = new NavGrid(width: 2, height: 2);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        Assert.Throws<ArgumentException>(() =>
            PathSmoothing.Smooth(ReadOnlySpan<int>.Empty, new long[1], new long[1], grid, wallBuckets));
    }

    [Fact]
    public void OutputSpanShorterThanTheCorridor_Throws()
    {
        var grid = new NavGrid(width: 3, height: 1);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var corridor = new[] { grid.CellIndex(0, 0), grid.CellIndex(1, 0), grid.CellIndex(2, 0) };

        Assert.Throws<ArgumentException>(() =>
            PathSmoothing.Smooth(corridor, new long[2], new long[2], grid, wallBuckets));
    }

    [Fact]
    public void MismatchedOutputSpanLengths_Throws()
    {
        var grid = new NavGrid(width: 2, height: 1);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var corridor = new[] { grid.CellIndex(0, 0), grid.CellIndex(1, 0) };

        Assert.Throws<ArgumentException>(() =>
            PathSmoothing.Smooth(corridor, new long[2], new long[3], grid, wallBuckets));
    }

    [Fact]
    public void NullGrid_Throws()
    {
        var grid = new NavGrid(width: 2, height: 1);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var corridor = new[] { grid.CellIndex(0, 0), grid.CellIndex(1, 0) };

        Assert.Throws<ArgumentNullException>(() =>
            PathSmoothing.Smooth(corridor, new long[2], new long[2], null!, wallBuckets));
    }

    [Fact]
    public void NullWallBuckets_Throws()
    {
        var grid = new NavGrid(width: 2, height: 1);
        var corridor = new[] { grid.CellIndex(0, 0), grid.CellIndex(1, 0) };

        Assert.Throws<ArgumentNullException>(() =>
            PathSmoothing.Smooth(corridor, new long[2], new long[2], grid, null!));
    }

    private static void AssertEverySegmentIsVisible(ReadOnlySpan<long> pointsX, ReadOnlySpan<long> pointsY, NavGrid grid, WallBuckets wallBuckets)
    {
        for (var i = 1; i < pointsX.Length; i++)
        {
            Assert.True(
                LineOfSight.IsVisible(pointsX[i - 1], pointsY[i - 1], pointsX[i], pointsY[i], grid, wallBuckets),
                $"Segment {i - 1}->{i} ({pointsX[i - 1]},{pointsY[i - 1]}) -> ({pointsX[i]},{pointsY[i]}) must pass line of sight.");
        }
    }
}
