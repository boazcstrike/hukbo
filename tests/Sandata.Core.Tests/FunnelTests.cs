using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="Funnel.StringPull"/>: a straight corridor
/// collapsing to two points, an L-shaped corridor collapsing to three, an
/// 18.4-degree corridor (design section 7's stated shallow angle,
/// <c>atan(1/3)</c>, the same slope <c>GridRayTests</c> pins) collapsing to
/// one straight segment rather than a staircase, and a check that the
/// licence header names the ported source and its licence.
/// </summary>
/// <remarks>
/// Every expected point sequence below was worked out by hand — walking the
/// funnel's apex/left/right state through each portal using the written
/// Recast/Detour algorithm and <c>ExactPredicates.Orient</c>'s definition —
/// never by reading a value back from a run of <see cref="Funnel"/> itself.
/// </remarks>
public sealed class FunnelTests
{
    /// <summary>
    /// Corridor (0,0) -&gt; (1,0) -&gt; (2,0), three cells in a single row.
    /// Cell centres are (2,2), (6,2), (10,2), all on the same horizontal
    /// line, so every portal the funnel visits leaves both bounds able to
    /// tighten all the way to the corridor's own start and end centres:
    /// the string never needs a third point.
    /// </summary>
    [Fact]
    public void StraightCorridor_CollapsesToTwoPoints()
    {
        var grid = new NavGrid(width: 3, height: 1);
        var corridor = new[]
        {
            grid.CellIndex(0, 0),
            grid.CellIndex(1, 0),
            grid.CellIndex(2, 0),
        };

        Span<long> outputX = new long[8];
        Span<long> outputY = new long[8];

        var count = Funnel.StringPull(corridor, outputX, outputY, grid);

        Assert.Equal(2, count);
        AssertPoint(2, 2, outputX, outputY, 0);
        AssertPoint(10, 2, outputX, outputY, 1);
    }

    /// <summary>
    /// Corridor (0,0) -&gt; (1,0) -&gt; (1,1) -&gt; (1,2) -&gt; (1,3): one
    /// east step then three south steps, an asymmetric right-angle L. Hand
    /// tracing the funnel (apex/left/right starting at (2,2), the east
    /// portal's corners (4,0)/(4,4), then three south portals' corners at
    /// x=4 and x=8) finds the right bound crossing the left bound exactly at
    /// the second south portal, committing (4,4) — the concave corner — as
    /// the middle point, then running straight from there to the corridor's
    /// end centre (6,14) without a further commit.
    /// </summary>
    [Fact]
    public void LShapedCorridor_CollapsesToThreePoints()
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

        Span<long> outputX = new long[8];
        Span<long> outputY = new long[8];

        var count = Funnel.StringPull(corridor, outputX, outputY, grid);

        Assert.Equal(3, count);
        AssertPoint(2, 2, outputX, outputY, 0);
        AssertPoint(4, 4, outputX, outputY, 1);
        AssertPoint(6, 14, outputX, outputY, 2);
    }

    /// <summary>
    /// Corridor (0,0) -&gt; (1,0) -&gt; (2,0) -&gt; (2,1) -&gt; (3,1) -&gt;
    /// (4,1) -&gt; (5,1) -&gt; (5,2) -&gt; (6,2): two east-east-south stair
    /// steps, matching design section 7's stated <c>atan(1/3)</c> ~= 18.4
    /// degree slope (dx = 24, dy = 8 in world units, the same 1:3 ratio
    /// <c>GridRayTests.ShallowEighteenPointFourDegreeLine...</c> pins) —
    /// chosen so that both south portals' left corners, (8,4) and (20,8),
    /// land exactly on the straight line from the start centre (2,2) to the
    /// end centre (26,10). Hand tracing the funnel against those corners
    /// finds every intermediate portal tightens without ever forcing a
    /// bound crossing, so the whole eight-transition staircase collapses to
    /// the two endpoints — one straight segment, not a staircase.
    /// </summary>
    [Fact]
    public void ShallowEighteenPointFourDegreeCorridor_CollapsesToOneStraightSegment()
    {
        var grid = new NavGrid(width: 7, height: 3);
        var corridor = new[]
        {
            grid.CellIndex(0, 0),
            grid.CellIndex(1, 0),
            grid.CellIndex(2, 0),
            grid.CellIndex(2, 1),
            grid.CellIndex(3, 1),
            grid.CellIndex(4, 1),
            grid.CellIndex(5, 1),
            grid.CellIndex(5, 2),
            grid.CellIndex(6, 2),
        };

        Span<long> outputX = new long[16];
        Span<long> outputY = new long[16];

        var count = Funnel.StringPull(corridor, outputX, outputY, grid);

        Assert.Equal(2, count);
        AssertPoint(2, 2, outputX, outputY, 0);
        AssertPoint(26, 10, outputX, outputY, 1);
    }

    /// <summary>
    /// Plan task 26's acceptance bar: <c>Funnel.cs</c>'s header must name the
    /// ported source (DotRecast) and its licence (zlib), so the attribution
    /// that licence requires cannot silently be deleted by a later edit.
    /// </summary>
    [Fact]
    public void LicenceHeaderNamesDotRecastAndZlib()
    {
        var path = FindSourceFile("Funnel.cs");
        var header = File.ReadAllText(path);

        Assert.Contains("DotRecast", header, StringComparison.Ordinal);
        Assert.Contains("zlib", header, StringComparison.Ordinal);
    }

    private static void AssertPoint(long expectedX, long expectedY, Span<long> outputX, Span<long> outputY, int index)
    {
        Assert.Equal(expectedX, outputX[index]);
        Assert.Equal(expectedY, outputY[index]);
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
