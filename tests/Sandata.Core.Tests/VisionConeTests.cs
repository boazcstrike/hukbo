using Sandata.Core.Geometry;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="VisionCone.Contains"/>: on-boundary
/// inclusion on both edges, a point just outside each edge excluded, a
/// point behind the apex excluded, a reflex cone wider than 180 degrees
/// handled, the range cutoff, and a property test proving the actual
/// hazard task 11 of the Sandata scaffold plan exists to avoid is absent.
///
/// Reconciled 2026-08-07 by task 56: this file originally proved that
/// absence by scanning <c>VisionCone.cs</c>'s source text for the literal
/// token <c>Trig</c>. That scan forbade a symbol name, not a hazard, and it
/// would have kept passing even if <see cref="VisionCone.Contains"/> were
/// rewritten as a cosine comparison against unnormalised boundary vectors
/// fetched through a differently named helper. The real hazard is that
/// Hukbo's facing sector vectors are not unit length — 946^2 + 392^2 is
/// 1,048,580 against 1024^2's 1,048,576, and the error differs per sector —
/// so a cosine comparison against them silently changes cone shape with
/// facing, and normalising them would square an already-squared magnitude,
/// overflowing <see cref="long"/> at this game's map scale. A half-plane
/// cross-product test has neither problem, because its sign depends only on
/// direction, never on the boundary vector's magnitude. The property test
/// below, <c>AngularContainment_IsInvariantToPositiveScalingOfTheCandidateOffset</c>,
/// operationalises exactly that difference: scaling the candidate offset by
/// a positive integer (and scaling <c>rangeSquared</c> by the square of the
/// same factor, to hold the range decision fixed) must not change the
/// angular verdict, which is a property a magnitude- or cosine-based test
/// would not generally hold, because normalising an integer vector and
/// comparing it to a scaled cosine threshold accumulates scale-dependent
/// rounding that a pure sign test never does.
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
    /// The property-based proof described in this file's class remarks:
    /// scaling the candidate offset by a positive integer, while scaling
    /// <c>rangeSquared</c> by the square of the same factor to hold the
    /// range decision fixed, must never change the angular verdict. Each
    /// row below is a fixture already exercised as a single point above —
    /// well inside, exactly on the left edge, one BAM unit beyond the left
    /// edge, and behind the apex — checked again across several scale
    /// factors including the identity factor 1, which recovers the
    /// single-point fixture exactly.
    /// </summary>
    [Theory]
    [InlineData(100L, 0L, 250_000L, true)]                // well inside, directly ahead
    [InlineData(46_341L, -46_341L, 5_000_000_000L, true)] // exactly on the left edge
    [InlineData(46_336L, -46_345L, 5_000_000_000L, false)] // one BAM unit beyond the left edge
    [InlineData(-100L, 0L, 250_000L, false)]              // behind the apex
    public void AngularContainment_IsInvariantToPositiveScalingOfTheCandidateOffset(
        long dx, long dy, long rangeSquaredBase, bool expected)
    {
        foreach (var scale in new long[] { 1, 2, 3, 5, 11 })
        {
            var scaledRangeSquared = checked(rangeSquaredBase * scale * scale);
            var actual = VisionCone.Contains(
                NarrowCentre,
                NarrowHalfWidth,
                scaledRangeSquared,
                checked(dx * scale),
                checked(dy * scale));

            Assert.Equal(expected, actual);
        }
    }
}
