using Sandata.Core.Geometry;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="RayBox.Intersects"/>: axis-parallel rays,
/// corner grazes, an origin already inside the box, and the division-free
/// source-scan proof required by task 6 of the Sandata scaffold plan.
/// </summary>
public class RayBoxTests
{
    private const long MinX = 2;
    private const long MinY = 2;
    private const long MaxX = 8;
    private const long MaxY = 8;

    [Fact]
    public void AxisParallelRayAlongX_CrossingTheBoxRow_Intersects()
    {
        // Direction (1, 0): parallel to X, at Y = 5 which lies inside [2, 8].
        Assert.True(RayBox.Intersects(0, 5, 1, 0, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void AxisParallelRayAlongX_OutsideTheBoxRow_DoesNotIntersect()
    {
        // Same direction, but Y = 10 lies outside [2, 8], so the ray can
        // never enter the box no matter how far along X it travels.
        Assert.False(RayBox.Intersects(0, 10, 1, 0, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void AxisParallelRayAlongY_CrossingTheBoxColumn_Intersects()
    {
        // Direction (0, 1): parallel to Y, at X = 5 which lies inside [2, 8].
        Assert.True(RayBox.Intersects(5, 0, 0, 1, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void AxisParallelRayAlongY_OutsideTheBoxColumn_DoesNotIntersect()
    {
        Assert.False(RayBox.Intersects(10, 0, 0, 1, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void DiagonalRay_GrazesExactlyOneCorner_Intersects()
    {
        // Origin (0, 0), direction (1, 1), box [2, 4] x [4, 6]: the diagonal
        // t = 4 is the only parameter satisfying both slabs, landing exactly
        // on the box's single corner (4, 4). The touch is inclusive.
        Assert.True(RayBox.Intersects(0, 0, 1, 1, 2, 4, 4, 6));
    }

    [Fact]
    public void DiagonalRay_JustMissesTheCorner_DoesNotIntersect()
    {
        // Shifting the box up by one unit moves the Y slab's entry
        // parameter past the X slab's exit parameter, so the two slabs no
        // longer overlap at any t.
        Assert.False(RayBox.Intersects(0, 0, 1, 1, 2, 5, 4, 7));
    }

    [Fact]
    public void OriginInsideBox_WithNonZeroDirection_Intersects()
    {
        Assert.True(RayBox.Intersects(5, 5, 1, 1, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void OriginInsideBox_WithZeroDirection_Intersects()
    {
        // A zero-length direction degenerates the ray to a stationary
        // point; the point is inside the box on both axes.
        Assert.True(RayBox.Intersects(5, 5, 0, 0, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void OriginOutsideBox_WithZeroDirection_DoesNotIntersect()
    {
        Assert.False(RayBox.Intersects(10, 5, 0, 0, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void DiagonalRay_ThroughTheBoxInterior_Intersects()
    {
        Assert.True(RayBox.Intersects(0, 0, 1, 1, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void DiagonalRay_BoxBehindOrigin_DoesNotIntersect()
    {
        // Direction (1, 1) points away from the box, which sits at lower
        // coordinates than the origin: the box lies at negative t only.
        Assert.False(RayBox.Intersects(20, 20, 1, 1, MinX, MinY, MaxX, MaxY));
    }

    [Fact]
    public void NegativeDirection_ReachesBoxBehindOrigin_Intersects()
    {
        // Reversing the direction from the previous case makes the box
        // reachable at positive t.
        Assert.True(RayBox.Intersects(20, 20, -1, -1, MinX, MinY, MaxX, MaxY));
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
        var path = FindSourceFile("RayBox.cs");
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
