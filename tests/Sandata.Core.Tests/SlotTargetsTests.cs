using System.Collections.Immutable;
using Sandata.Core.Navigation;
using Sandata.Core.Squads;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 34 of docs/plans/2026-08-07-sandata-scaffold.md: arclength slot
/// targeting. Design section 8, "Arclength slot offsets", is the contract
/// under test — a follower's target is a pure function of its slot's trail
/// offset and lateral offset along the shared polyline's precomputed
/// cumulative arclength, so it stands on the leader's past path rather than
/// at a position built from the leader's current heading.
/// </summary>
public sealed class SlotTargetsTests
{
    /// <summary>
    /// A right-angle corridor: east from the origin, then a 90-degree turn
    /// south, matching design section 8's "cuts the same corners" claim. Both
    /// legs are exactly 100 world units, chosen so each segment's Euclidean
    /// length is an exact integer (a 3-4-5-style right triangle is not even
    /// needed here — an axis-aligned segment's length is already exact) and
    /// no rounding enters the arithmetic this test hand-verifies.
    /// </summary>
    private static ImmutableArray<PathPoint> RightAngleCorridorPolyline() =>
    [
        new PathPoint(0, 0),
        new PathPoint(100, 0),
        new PathPoint(100, 100),
    ];

    [Fact]
    public void Build_ComputesCumulativeArclength_AsTrueEuclideanLength()
    {
        // (0,0) -> (3,4) is a 3-4-5 right triangle: Euclidean length 5, not
        // the 7 that a Manhattan (squared-free) measure would report. This
        // pins that PolylineArclength chose the true distance, per its own
        // remarks on why that measure is required.
        ImmutableArray<PathPoint> polyline =
        [
            new PathPoint(0, 0),
            new PathPoint(3, 4),
            new PathPoint(3, -4),
        ];

        var arclength = PolylineArclength.Build(polyline);

        Assert.Equal(0L, arclength.ArclengthAtVertex(0));
        Assert.Equal(5L, arclength.ArclengthAtVertex(1));
        Assert.Equal(13L, arclength.ArclengthAtVertex(2));
        Assert.Equal(13L, arclength.TotalLength);
    }

    [Fact]
    public void FloorVertexIndex_ExactVertexQuery_ReturnsThatVertexIndex()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        for (var vertexIndex = 0; vertexIndex < arclength.VertexCount; vertexIndex++)
        {
            var exactArclength = arclength.ArclengthAtVertex(vertexIndex);

            Assert.Equal(vertexIndex, arclength.FloorVertexIndex(exactArclength));
        }
    }

    [Fact]
    public void SampleAt_MidSegment_InterpolatesLinearlyAndReportsSegmentDirection()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        var sample = arclength.SampleAt(50);

        Assert.Equal(50L, sample.X);
        Assert.Equal(0L, sample.Y);
        Assert.Equal(100L, sample.DirectionX);
        Assert.Equal(0L, sample.DirectionY);
        Assert.Equal(100L, sample.DirectionLength);
    }

    [Fact]
    public void SampleAt_BeyondTotalLength_ClampsToTheFinalVertex()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        var sample = arclength.SampleAt(arclength.TotalLength + 1_000);

        Assert.Equal(100L, sample.X);
        Assert.Equal(100L, sample.Y);
    }

    [Fact]
    public void SampleAt_BeforeStart_ClampsToTheFirstVertex()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        var sample = arclength.SampleAt(-1_000);

        Assert.Equal(0L, sample.X);
        Assert.Equal(0L, sample.Y);
    }

    /// <summary>
    /// The acceptance test design section 8 exists to satisfy: on a
    /// right-angle corner, a follower's arclength-based target stays inside
    /// the corridor, in a case where a rigid world-space lateral offset —
    /// one direction vector, taken from the leader's current heading and
    /// applied without regard to where the corridor has since turned — would
    /// place that same follower inside a wall. Both are asserted so the test
    /// demonstrates the difference rather than only the good case.
    /// </summary>
    [Fact]
    public void ComputeTarget_OnRightAngleCorner_StaysInCorridor_WhereRigidWorldSpaceOffsetWouldNot()
    {
        var polyline = RightAngleCorridorPolyline();
        var path = PolylineArclength.Build(polyline);

        const long leaderArclength = 200; // the leader has walked the full corridor: it stands at (100, 100), heading south.
        const long trailOffset = 150; // this follower's own position is 150 units behind the leader, along the path: arclength 50, still on the east-heading leg.
        const long lateralOffset = 15; // half the formation width; well inside the corridor's own half-width of 20.

        // The correct, arclength-based target: the follower's own point on
        // the path (50, 0), offset perpendicular to *that point's own*
        // direction of travel (east), which is a shift in Y.
        var correctTarget = SlotTargets.ComputeTarget(path, leaderArclength, trailOffset, lateralOffset);
        Assert.Equal((50L, -15L), correctTarget);

        // The rigid, world-space alternative a naive "offset from the
        // leader" implementation would compute: walk straight backward from
        // the leader's own position along the leader's own current heading
        // (south), and offset perpendicular to *that* heading (a shift in
        // X) — never consulting the fact that the path bent before this
        // follower's own position on it.
        var leaderSample = path.SampleAt(leaderArclength);
        var rigidLongitudinalX = leaderSample.X - (leaderSample.DirectionX * trailOffset / leaderSample.DirectionLength);
        var rigidLongitudinalY = leaderSample.Y - (leaderSample.DirectionY * trailOffset / leaderSample.DirectionLength);
        var rigidLateralX = leaderSample.DirectionY * lateralOffset / leaderSample.DirectionLength;
        var rigidLateralY = -leaderSample.DirectionX * lateralOffset / leaderSample.DirectionLength;
        var rigidTarget = (X: rigidLongitudinalX + rigidLateralX, Y: rigidLongitudinalY + rigidLateralY);
        Assert.Equal((115L, -50L), rigidTarget);

        // A corridor of half-width 20 centred on the polyline: the union of
        // the horizontal leg's slab and the vertical leg's slab.
        static bool IsInsideCorridor(long x, long y) =>
            (x is >= -20 and <= 100 && y is >= -20 and <= 20) ||
            (x is >= 80 and <= 120 && y is >= -20 and <= 120);

        Assert.True(IsInsideCorridor(correctTarget.X, correctTarget.Y));
        Assert.False(IsInsideCorridor(rigidTarget.X, rigidTarget.Y));
    }

    [Fact]
    public void ComputeTarget_ZeroLateralOffset_LeavesTheTargetOnTheCentreline()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        var target = SlotTargets.ComputeTarget(path, leaderArclength: 200, trailOffset: 150, lateralOffset: 0);

        Assert.Equal((50L, 0L), target);
    }

    [Fact]
    public void ComputeTarget_TrailOffsetPastTheStart_ClampsRatherThanExtrapolating()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        var target = SlotTargets.ComputeTarget(path, leaderArclength: 50, trailOffset: 10_000, lateralOffset: 0);

        Assert.Equal((0L, 0L), target);
    }

    [Fact]
    public void ComputeTarget_NegativeTrailOffset_Throws()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlotTargets.ComputeTarget(path, leaderArclength: 100, trailOffset: -1, lateralOffset: 0));
    }

    /// <summary>
    /// Design section 8's "cumulative integer arclength" has to be a pure
    /// function of the polyline: building the table twice from the same
    /// polyline value, and evaluating the same slot against each, must
    /// produce bit-identical targets — the acceptance criterion "slot
    /// targets are identical across two evaluations of the same polyline."
    /// </summary>
    [Fact]
    public void ComputeTarget_TwoEvaluationsOfTheSamePolyline_ProduceIdenticalTargets()
    {
        var polyline = RightAngleCorridorPolyline();

        var firstPath = PolylineArclength.Build(polyline);
        var secondPath = PolylineArclength.Build(polyline);

        for (long trailOffset = 0; trailOffset <= 200; trailOffset += 17)
        {
            var first = SlotTargets.ComputeTarget(firstPath, leaderArclength: 200, trailOffset, lateralOffset: 12);
            var second = SlotTargets.ComputeTarget(secondPath, leaderArclength: 200, trailOffset, lateralOffset: 12);

            Assert.Equal(first, second);
        }
    }
}
