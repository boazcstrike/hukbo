using Sandata.Core.Geometry;

namespace Sandata.Core.Tests;

public sealed class ExactPredicatesTests
{
    // --- Orient: sign only, both turn directions, collinear, magnitude boundary ---

    [Fact]
    public void Orient_CounterClockwiseTurn_ReturnsPositiveOne()
    {
        Assert.Equal(1, ExactPredicates.Orient(0, 0, 10, 0, 5, 5));
    }

    [Fact]
    public void Orient_ClockwiseTurn_ReturnsNegativeOne()
    {
        Assert.Equal(-1, ExactPredicates.Orient(0, 0, 10, 0, 5, -5));
    }

    [Fact]
    public void Orient_CollinearPoint_ReturnsZero()
    {
        Assert.Equal(0, ExactPredicates.Orient(0, 0, 10, 0, 20, 0));
    }

    [Fact]
    public void Orient_AtMaximumMapCoordinateMagnitude_ReturnsCorrectSignWithoutOverflow()
    {
        // 8,388,608 is the map's maximum raw coordinate (2^23). The
        // intermediate cross product here is 8,388,608 * -8,388,608 =
        // -70,368,744,177,664, which overflows a 32-bit accumulator by a
        // wide margin but sits comfortably inside `long` range. This pins
        // exactly the boundary the design's coordinate budget describes.
        const long maxCoordinate = 8_388_608;

        int sign = ExactPredicates.Orient(0, 0, maxCoordinate, 0, 0, -maxCoordinate);

        Assert.Equal(-1, sign);
    }

    [Fact]
    public void Orient_AtMaximumMapCoordinateMagnitude_OppositeWinding_ReturnsPositiveOne()
    {
        const long maxCoordinate = 8_388_608;

        int sign = ExactPredicates.Orient(0, 0, maxCoordinate, 0, 0, maxCoordinate);

        Assert.Equal(1, sign);
    }

    // --- ClassifySegments: one pinned vector per named result ---

    [Fact]
    public void ClassifySegments_LinesCrossOutsideBothExtents_ReturnsDisjoint()
    {
        // A-B is the segment (0,0)-(10,0). C-D is the vertical segment
        // (20,-5)-(20,5). The infinite lines through each segment do cross,
        // but the crossing point (20, 0) lies beyond B on line A-B, so the
        // segments themselves never meet.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            20, -5, 20, 5);

        Assert.Equal(SegmentRelation.Disjoint, relation);
    }

    [Fact]
    public void ClassifySegments_ParallelOffsetSegments_ReturnsDisjoint()
    {
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            0, 5, 10, 5);

        Assert.Equal(SegmentRelation.Disjoint, relation);
    }

    [Fact]
    public void ClassifySegments_ClassicXCrossing_ReturnsCrossing()
    {
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 10,
            0, 10, 10, 0);

        Assert.Equal(SegmentRelation.Crossing, relation);
    }

    [Fact]
    public void ClassifySegments_CrossingBothTurnDirections_ReturnsCrossing()
    {
        // The mirror image of the classic X, confirming the crossing test
        // is symmetric under reflection rather than tuned to one winding.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 10, 10, 0,
            0, 0, 10, 10);

        Assert.Equal(SegmentRelation.Crossing, relation);
    }

    [Fact]
    public void ClassifySegments_TJunction_EndpointOnOtherSegmentInterior_ReturnsTouching()
    {
        // C = (5, 0) lands exactly on the interior of A-B = (0,0)-(10,0).
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            5, 0, 5, 10);

        Assert.Equal(SegmentRelation.Touching, relation);
    }

    [Fact]
    public void ClassifySegments_SharedEndpoint_ReturnsTouching()
    {
        // B = (10, 0) and C = (10, 0) are the same point.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            10, 0, 10, 10);

        Assert.Equal(SegmentRelation.Touching, relation);
    }

    [Fact]
    public void ClassifySegments_CollinearSegmentsMeetingAtOnePoint_ReturnsTouching()
    {
        // Both segments lie on y = 0. A-B ends at x = 10, C-D starts at
        // x = 10: the collinear intervals meet at exactly one point.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            10, 0, 20, 0);

        Assert.Equal(SegmentRelation.Touching, relation);
    }

    [Fact]
    public void ClassifySegments_CollinearOverlappingIntervals_ReturnsCollinearOverlap()
    {
        // Both segments lie on y = 0. [0,10] and [5,15] overlap on [5,10].
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            5, 0, 15, 0);

        Assert.Equal(SegmentRelation.CollinearOverlap, relation);
    }

    [Fact]
    public void ClassifySegments_CollinearVerticalOverlappingIntervals_ReturnsCollinearOverlap()
    {
        // The same overlap, but on a vertical line, exercising the y-axis
        // branch of the collinear parametrization.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 0, 10,
            0, 5, 0, 15);

        Assert.Equal(SegmentRelation.CollinearOverlap, relation);
    }

    [Fact]
    public void ClassifySegments_CollinearNonOverlappingIntervals_ReturnsDisjoint()
    {
        // Both segments lie on y = 0. [0,10] and [20,30] never meet.
        SegmentRelation relation = ExactPredicates.ClassifySegments(
            0, 0, 10, 0,
            20, 0, 30, 0);

        Assert.Equal(SegmentRelation.Disjoint, relation);
    }

    [Fact]
    public void ClassifySegments_AtMaximumMapCoordinateMagnitude_CrossingResolvesWithoutOverflow()
    {
        // The classic X, scaled to the map's maximum raw coordinate, so the
        // orientation tests inside ClassifySegments exercise the same
        // magnitude boundary as the direct Orient tests above.
        const long maxCoordinate = 8_388_608;

        SegmentRelation relation = ExactPredicates.ClassifySegments(
            -maxCoordinate, -maxCoordinate, maxCoordinate, maxCoordinate,
            -maxCoordinate, maxCoordinate, maxCoordinate, -maxCoordinate);

        Assert.Equal(SegmentRelation.Crossing, relation);
    }

    [Fact]
    public void ClassifySegments_DegenerateFirstSegment_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExactPredicates.ClassifySegments(
            5, 5, 5, 5,
            0, 0, 10, 10));
    }

    [Fact]
    public void ClassifySegments_DegenerateSecondSegment_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExactPredicates.ClassifySegments(
            0, 0, 10, 10,
            5, 5, 5, 5));
    }
}
