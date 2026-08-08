using Sandata.Core.Geometry;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="Polygon.Contains"/>: a vertex hit, an edge
/// hit, a horizontal edge, a ray exiting through a vertex, and the
/// division-free source-scan proof required by task 6 of the Sandata
/// scaffold plan.
/// </summary>
public class PolygonTests
{
    // A 10x10 axis-aligned square: (0,0) -> (10,0) -> (10,10) -> (0,10).
    private static readonly long[] SquareXs = [0, 10, 10, 0];
    private static readonly long[] SquareYs = [0, 0, 10, 10];

    // A notch pentagon whose middle vertex (2, 2) points inward, so a
    // horizontal ray at Y = 2 exits the polygon exactly through that vertex:
    // (0,0) -> (4,0) -> (4,4) -> (2,2) -> (0,4).
    private static readonly long[] NotchXs = [0, 4, 4, 2, 0];
    private static readonly long[] NotchYs = [0, 0, 4, 2, 4];

    [Fact]
    public void PointStrictlyInsideTheSquare_ReturnsTrue()
    {
        Assert.True(Polygon.Contains(SquareXs, SquareYs, 5, 5));
    }

    [Fact]
    public void PointStrictlyOutsideTheSquare_ReturnsFalse()
    {
        Assert.False(Polygon.Contains(SquareXs, SquareYs, 20, 20));
    }

    [Fact]
    public void VertexHit_IncludedByTheHalfOpenRule_ReturnsTrue()
    {
        // The bottom-left corner (0, 0) is the vertex where the closing
        // edge's "<=" endpoint meets the first edge's "<=" endpoint at
        // Y = 0; tracing the half-open rule through every edge lands on an
        // odd crossing count for this corner.
        Assert.True(Polygon.Contains(SquareXs, SquareYs, 0, 0));
    }

    [Fact]
    public void VertexHit_ExcludedByTheHalfOpenRule_ReturnsFalse()
    {
        // The top-right corner (10, 10) sits at the strict "<" endpoint of
        // both edges that meet there, so neither edge's half-open test ever
        // fires for it and the crossing count is zero. The half-open rule
        // is deliberately asymmetric between a polygon's two "kinds" of
        // vertex, which is exactly why one corner of the same square
        // returns true and the other returns false.
        Assert.False(Polygon.Contains(SquareXs, SquareYs, 10, 10));
    }

    [Fact]
    public void EdgeHit_OnTheRightVerticalEdge_ReturnsFalse()
    {
        // (10, 5) sits exactly on the vertical edge from (10, 0) to
        // (10, 10). That edge's cross product against the point is exactly
        // zero (collinear), which is neither strictly positive nor strictly
        // negative, so the edge never toggles the crossing count and no
        // other edge is a candidate at Y = 5.
        Assert.False(Polygon.Contains(SquareXs, SquareYs, 10, 5));
    }

    [Fact]
    public void HorizontalEdgeHit_OnTheBottomEdge_ReturnsTrue()
    {
        // (5, 0) sits exactly on the horizontal bottom edge. That edge has
        // ay == by, so it fails both half-open height tests unconditionally
        // and contributes nothing; the answer here is decided entirely by
        // the two vertical edges, which resolve to a single crossing.
        Assert.True(Polygon.Contains(SquareXs, SquareYs, 5, 0));
    }

    [Fact]
    public void HorizontalEdgeHit_OnTheTopEdge_ReturnsFalse()
    {
        Assert.False(Polygon.Contains(SquareXs, SquareYs, 5, 10));
    }

    [Fact]
    public void RayExitsThroughAVertex_LeftOfTheNotch_ReturnsTrue()
    {
        // At Y = 2, the horizontal ray from (1, 2) passes exactly through
        // the notch pentagon's inward vertex (2, 2), where two edges meet.
        // Each adjoining edge is evaluated by its own independent
        // half-open test rather than a shared "is this height on the
        // boundary" branch, so the vertex is neither double-counted nor
        // dropped: one edge above the notch and one edge to its right each
        // contribute a crossing along with the ray's other boundary
        // crossing, for an odd total.
        Assert.True(Polygon.Contains(NotchXs, NotchYs, 1, 2));
    }

    [Fact]
    public void RayExitsThroughAVertex_RightOfTheNotch_ReturnsTrue()
    {
        Assert.True(Polygon.Contains(NotchXs, NotchYs, 3, 2));
    }

    /// <summary>
    /// Task 6's test bar requires proving, not just asserting, that this
    /// file never divides a parametric value. This scans the file's own
    /// non-comment source lines for a division operator, mirroring the
    /// comment-skipping rule <c>SourceHygieneTests</c> already uses for its
    /// banned-token scans.
    /// </summary>
    [Fact]
    public void SourceContainsNoDivisionOfAParametricValue()
    {
        var path = FindSourceFile("Polygon.cs");
        var offendingLines = FindLinesContainingDivision(path);

        Assert.Empty(offendingLines);
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
                var found = Path.Combine(directory.FullName, "src", "Sandata.Core", "Geometry", fileName);
                Assert.True(File.Exists(found), "Expected to find " + found);
                return found;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (Hukbo.slnx) above " + AppContext.BaseDirectory);
    }
}
