using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="GridRay.Traverse"/>: an axis-parallel line,
/// an exact diagonal through a corner (pinning the "step X first" tie
/// rule), and a shallow 18.4-degree line, plus the division-free source-scan
/// proof plan task 20 requires.
/// </summary>
/// <remarks>
/// Every expected cell sequence below was worked out independently of the
/// implementation, by hand-tracing the Amanatides-Woo walk against the
/// world-to-cell floor-division rule, never by reading a value back from a
/// run of <see cref="GridRay"/> itself.
/// </remarks>
public sealed class GridRayTests
{
    [Fact]
    public void AxisParallelLine_AlongX_VisitsEveryCellInTheRow()
    {
        // Origin (0, 2) and target (16, 2) both sit in row 0 (2 // 4 == 0),
        // so the whole walk is a straight run along X: cells 0 through 4.
        var grid = new NavGrid(width: 6, height: 2);
        Span<int> cells = new int[8];

        var count = GridRay.Traverse(originX: 0, originY: 2, targetX: 16, targetY: 2, cells, grid);

        var expected = new[] { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) };
        AssertVisitedSequence(grid, expected, cells, count);
    }

    [Fact]
    public void ExactDiagonalThroughACorner_AlternatesXThenYPerTheWrittenTieRule()
    {
        // Origin (0, 0) to target (16, 16) is the exact diagonal: every
        // step crosses a vertical and a horizontal grid line at the same
        // parameter. The written rule resolves every such tie by stepping
        // X first, which is what produces this alternating staircase rather
        // than a jump straight to each diagonal neighbour.
        var grid = new NavGrid(width: 6, height: 6);
        Span<int> cells = new int[16];

        var count = GridRay.Traverse(originX: 0, originY: 0, targetX: 16, targetY: 16, cells, grid);

        var expected = new[]
        {
            (0, 0), (1, 0), (1, 1), (2, 1), (2, 2), (3, 2), (3, 3), (4, 3), (4, 4),
        };
        AssertVisitedSequence(grid, expected, cells, count);
    }

    [Fact]
    public void ShallowEighteenPointFourDegreeLine_VisitsTheHandTracedStaircase()
    {
        // Origin (0, 0) to target (48, 16): dy/dx is exactly 1/3, which is
        // atan(1/3) ~= 18.43 degrees — the shallow angle design section 7
        // names as the case where supercover cell membership and the drawn
        // wall geometry visibly disagree (see LineOfSightTests).
        var grid = new NavGrid(width: 13, height: 5);
        Span<int> cells = new int[20];

        var count = GridRay.Traverse(originX: 0, originY: 0, targetX: 48, targetY: 16, cells, grid);

        var expected = new[]
        {
            (0, 0), (1, 0), (2, 0), (3, 0), (3, 1), (4, 1), (5, 1), (6, 1),
            (6, 2), (7, 2), (8, 2), (9, 2), (9, 3), (10, 3), (11, 3), (12, 3), (12, 4),
        };
        AssertVisitedSequence(grid, expected, cells, count);
    }

    [Fact]
    public void OriginAndTargetInTheSameCell_ReturnsOnlyThatOneCell()
    {
        var grid = new NavGrid(width: 4, height: 4);
        Span<int> cells = new int[4];

        var count = GridRay.Traverse(originX: 1, originY: 1, targetX: 3, targetY: 3, cells, grid);

        Assert.Equal(1, count);
        Assert.Equal(grid.CellIndex(0, 0), cells[0]);
    }

    [Fact]
    public void DestinationSpanShorterThanTheFullWalk_StopsEarlyWithoutOverrunning()
    {
        // The exact diagonal from the corner test above visits nine cells;
        // a three-cell buffer must receive exactly the first three and no
        // more, rather than throwing or writing past its end.
        var grid = new NavGrid(width: 6, height: 6);
        Span<int> cells = new int[3];

        var count = GridRay.Traverse(originX: 0, originY: 0, targetX: 16, targetY: 16, cells, grid);

        Assert.Equal(3, count);
        Assert.Equal(grid.CellIndex(0, 0), cells[0]);
        Assert.Equal(grid.CellIndex(1, 0), cells[1]);
        Assert.Equal(grid.CellIndex(1, 1), cells[2]);
    }

    [Fact]
    public void LineExitsGridBounds_StopsAtTheLastInBoundsCellInsteadOfThrowing()
    {
        // The target's own cell (10, 0) lies far outside this 2 by 2 grid;
        // the walk must stop the moment it leaves bounds rather than
        // throwing or trying to report an out-of-range cell index.
        var grid = new NavGrid(width: 2, height: 2);
        Span<int> cells = new int[8];

        var count = GridRay.Traverse(originX: 0, originY: 0, targetX: 40, targetY: 0, cells, grid);

        Assert.Equal(2, count);
        Assert.Equal(grid.CellIndex(0, 0), cells[0]);
        Assert.Equal(grid.CellIndex(1, 0), cells[1]);
    }

    [Fact]
    public void OriginCellOutsideTheGrid_Throws()
    {
        var grid = new NavGrid(width: 2, height: 2);
        var cells = new int[4];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridRay.Traverse(originX: 100, originY: 100, targetX: 0, targetY: 0, cells, grid));
    }

    [Fact]
    public void EmptyDestinationSpan_Throws()
    {
        var grid = new NavGrid(width: 2, height: 2);

        Assert.Throws<ArgumentException>(
            () => GridRay.Traverse(originX: 0, originY: 0, targetX: 4, targetY: 4, Span<int>.Empty, grid));
    }

    /// <summary>
    /// Task 20's test bar: this file must never divide a parametric value.
    /// Mirrors the scan <c>RayBoxTests.SourceContainsNoDivisionOfAParametricValue</c>
    /// already runs against <c>RayBox.cs</c> for the same requirement.
    /// </summary>
    [Fact]
    public void SourceContainsNoDivisionOfAParametricValue()
    {
        var path = FindSourceFile("GridRay.cs");
        var offendingLines = FindLinesContainingDivision(path);

        Assert.Empty(offendingLines);
    }

    private static void AssertVisitedSequence(NavGrid grid, (int X, int Y)[] expected, Span<int> cells, int count)
    {
        Assert.Equal(expected.Length, count);

        for (var index = 0; index < expected.Length; index++)
        {
            var (expectedX, expectedY) = expected[index];
            Assert.Equal(grid.CellIndex(expectedX, expectedY), cells[index]);
        }
    }

    private static string[] FindLinesContainingDivision(string path)
    {
        var offenders = new List<string>();
        var lines = File.ReadAllLines(path);

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (lines[index].Contains('/'))
            {
                offenders.Add(path + ":" + (index + 1));
            }
        }

        return offenders.ToArray();
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidateRoot = Path.Combine(directory.FullName, "Hukbo.slnx");
            if (File.Exists(candidateRoot))
            {
                var found = Path.Combine(directory.FullName, "src", "Sandata.Core", "Navigation", fileName);
                Assert.True(File.Exists(found), "Expected to find " + found);
                return found;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (Hukbo.slnx) above " + AppContext.BaseDirectory);
    }
}
