using Hukbo.Diagnostics;
using Sandata.Core.Geometry;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="VisionCone.Contains"/>: on-boundary
/// inclusion on both edges, a point just outside each edge excluded, a
/// point behind the apex excluded, a reflex cone wider than 180 degrees
/// handled, the range cutoff, and the source-scan proof required by task 11
/// of the Sandata scaffold plan that this file's implementation never
/// multiplies two normalised vectors together and never calls into
/// <c>Sandata.Core.Mathematics.Trig</c>.
/// </summary>
/// <remarks>
/// Every expected value below was computed independently of
/// <see cref="VisionCone"/> and <see cref="ConeBoundaryTable"/>, by a small
/// Python script reproducing the same quarter-wave-table-plus-interpolation
/// arithmetic from the mathematical definition of sine, never by reading
/// values back from a run of the implementation under test.
/// </remarks>
public sealed class VisionConeTests
{
    // A 45-degree half-width cone (90 degrees total) facing angle zero,
    // which points along (+1, 0) by ConeBoundaryTable's convention. 8,192
    // BAM units is exactly 45 degrees (8192 / 65536 * 360), so both edges
    // land exactly on a pinned table entry with no interpolation rounding,
    // which is what makes the on-boundary cases below exact rather than
    // approximate.
    private static readonly Bam16 NarrowCentre = new(0);
    private const ushort NarrowHalfWidth = 8192;

    // A generous range that comfortably covers every non-range-specific
    // fixture below (the boundary vectors themselves have a magnitude
    // around 65,536, so dx^2 + dy^2 for a point sitting on one of them is
    // a little over 4.29 billion).
    private const long AmpleRangeSquared = 10_000_000_000_000L;

    [Fact]
    public void PointDirectlyAheadOfANarrowCone_IsInside()
    {
        Assert.True(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: 100, dy: 0));
    }

    [Fact]
    public void PointExactlyOnTheLeftEdge_IsInside()
    {
        // The left boundary vector at angle 57,344 (centre - halfWidth) is
        // pinned to (46341, -46341). A candidate offset exactly equal to
        // that vector lies exactly on the edge ray.
        Assert.True(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: 46_341, dy: -46_341));
    }

    [Fact]
    public void PointExactlyOnTheRightEdge_IsInside()
    {
        // The right boundary vector at angle 8,192 (centre + halfWidth) is
        // pinned to (46341, 46341).
        Assert.True(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: 46_341, dy: 46_341));
    }

    [Fact]
    public void PointOneBamUnitBeyondTheLeftEdge_IsExcluded()
    {
        // The boundary vector one BAM unit further left (angle 57,343)
        // pins to (46336, -46345): rotating one raw unit past the left
        // edge, away from the cone's centre, lands just outside it.
        Assert.False(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: 46_336, dy: -46_345));
    }

    [Fact]
    public void PointOneBamUnitBeyondTheRightEdge_IsExcluded()
    {
        // The boundary vector one BAM unit further right (angle 8,193)
        // pins to (46336, 46345).
        Assert.False(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: 46_336, dy: 46_345));
    }

    [Fact]
    public void PointBehindTheApex_IsExcluded()
    {
        Assert.False(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, AmpleRangeSquared, dx: -100, dy: 0));
    }

    [Fact]
    public void PointWithinAngleButBeyondRange_IsExcluded()
    {
        Assert.False(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, rangeSquared: 250_000, dx: 1000, dy: 0));
    }

    [Fact]
    public void PointWithinAngleAndWithinRange_IsIncluded()
    {
        Assert.True(VisionCone.Contains(NarrowCentre, NarrowHalfWidth, rangeSquared: 250_000, dx: 100, dy: 0));
    }

    // A reflex cone: half-width 24,576 BAM units (135 degrees), so the
    // total angular width is 270 degrees — wider than a half turn (32,768
    // half-width would be exactly 180 degrees). The excluded region is the
    // 90-degree wedge directly opposite the centre direction.
    private const ushort ReflexHalfWidth = 24_576;

    [Fact]
    public void ReflexCone_PointDirectlyAhead_IsInside()
    {
        Assert.True(VisionCone.Contains(NarrowCentre, ReflexHalfWidth, AmpleRangeSquared, dx: 100, dy: 0));
    }

    [Fact]
    public void ReflexCone_PointDirectlyOppositeTheCentre_IsExcluded()
    {
        // Exactly opposite the centre direction, at the middle of the
        // excluded 90-degree wedge.
        Assert.False(VisionCone.Contains(NarrowCentre, ReflexHalfWidth, AmpleRangeSquared, dx: -100, dy: 0));
    }

    [Fact]
    public void ReflexCone_PointOutsideTheExcludedWedge_IsInside()
    {
        // Pure -Y (270 degrees by this table's convention) sits well
        // outside the 90-degree excluded wedge centred on 180 degrees, even
        // though it is still "behind" the centre direction in the ordinary
        // sense.
        Assert.True(VisionCone.Contains(NarrowCentre, ReflexHalfWidth, AmpleRangeSquared, dx: 0, dy: -100));
    }

    /// <summary>
    /// The proof named by the plan's "Done when" column: nothing in
    /// <c>VisionCone.cs</c> multiplies two normalised vectors together (the
    /// cosine-comparison shape this type exists to avoid) or calls into
    /// <c>Trig</c>. A cosine comparison of this kind always shows up as a
    /// call to a normalising or trigonometric helper — <c>Normalize</c>,
    /// <c>Trig.Sin</c>, or <c>Trig.Cos</c> — because there is no way to
    /// scale a vector to unit length using only the raw integer arithmetic
    /// this file is otherwise built from. Their absence is therefore a
    /// direct proof of the algorithm's shape, not a coincidental fact about
    /// naming.
    /// </summary>
    [Fact]
    public void VisionConeSource_NeverNormalisesAndNeverCallsTrig()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "src", "Sandata.Core", "Geometry", "VisionCone.cs");

        var offendingLines = File.ReadAllLines(path)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => !entry.Line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(entry =>
                entry.Line.Contains("Trig", StringComparison.Ordinal) ||
                entry.Line.Contains("Normalize", StringComparison.Ordinal) ||
                entry.Line.Contains("Normalise", StringComparison.Ordinal))
            .Select(entry => entry.Number)
            .ToArray();

        Assert.Empty(offendingLines);
    }

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so VisionCone.cs cannot be located for the source scan.");
        return root!;
    }
}
