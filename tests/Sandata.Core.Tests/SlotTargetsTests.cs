using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
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
    /// Every arclength, offset, and sampled coordinate in this file is in raw
    /// fixed-point units, because that is what <see cref="PolylineArclength"/>
    /// produces and consumes since task 87. The polyline *vertices* are still
    /// whole world units, because that is what <c>PathService</c> publishes.
    /// Writing the expectations through this helper keeps them readable as the
    /// world-unit distances a person actually reasons about, without letting a
    /// world-unit literal slip into a raw-unit position.
    /// </summary>
    private static long Raw(long worldUnits) => worldUnits * FixedPoint.Scale;

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

        // Every length below is raw fixed point, so a 3-4-5 triangle's
        // hypotenuse of 5 world units is 5 * FixedPoint.Scale. Both segments
        // are exact at raw scale, so this still pins the Euclidean measure
        // against the 7 a Manhattan measure would report, with no rounding
        // slack to hide behind.
        Assert.Equal(0L, arclength.ArclengthAtVertex(0));
        Assert.Equal(Raw(5), arclength.ArclengthAtVertex(1));
        Assert.Equal(Raw(13), arclength.ArclengthAtVertex(2));
        Assert.Equal(Raw(13), arclength.TotalLength);
    }

    /// <summary>
    /// A polyline carrying one segment of each kind the precision of this
    /// type has to survive: axis-aligned, an exact 45-degree diagonal, and an
    /// oblique segment at roughly 18.4 degrees — the angle of the fixture's
    /// own wall, and the case a squared-free measure gets wrong.
    /// </summary>
    private static ImmutableArray<PathPoint> MixedAnglePolyline() =>
    [
        new PathPoint(0, 0),
        new PathPoint(100, 0),
        new PathPoint(140, 40),
        new PathPoint(200, 60),
    ];

    /// <summary>
    /// Task 87's precision bar at its sharpest point: the stored length of a
    /// 45-degree segment is the truncated integer square root of a <b>raw</b>
    /// square, not the truncated root of a world-unit square scaled up
    /// afterwards. The two differ by more than two percent, and that
    /// difference is the whole defect.
    /// </summary>
    /// <remarks>
    /// An (8, 8) world-unit segment is (8,192, 8,192) raw, whose squared
    /// length is 134,217,728 and whose integer square root truncates to
    /// <b>11,585</b> against a true 8,192·√2 ≈ 11,585.24 — an error of one
    /// part in forty-eight thousand. Taking the root in world units first
    /// gives ⌊√128⌋ = 11, and scaling that to raw gives <b>11,264</b> — an
    /// error of one part in thirty-six, which is what stalled a leader
    /// aiming two world units ahead of itself. This test is the direct pin on
    /// that choice: it is the assertion that fails, alone and immediately,
    /// if anyone moves the scaling back to the far side of the square root.
    /// </remarks>
    [Fact]
    public void Build_DiagonalSegmentLength_IsTheRawRootNotTheScaledWorldUnitRoot()
    {
        ImmutableArray<PathPoint> polyline = [new PathPoint(0, 0), new PathPoint(8, 8)];

        var arclength = PolylineArclength.Build(polyline);

        Assert.Equal(11_585L, arclength.TotalLength);
        Assert.NotEqual(11L * FixedPoint.Scale, arclength.TotalLength);
    }

    /// <summary>
    /// Task 87's precision bar, first half: a sample taken at a vertex's own
    /// cumulative arclength lands on that vertex exactly, and a sample taken
    /// anywhere inside a segment lands on that segment to within one raw unit
    /// of perpendicular error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The perpendicular error is asserted as an exact integer cross product
    /// rather than a distance, so no square root and no tolerance constant
    /// enters the test: for a point <c>p</c> on the segment <c>a → b</c>, the
    /// perpendicular distance is <c>|(p - a) × (b - a)| / |b - a|</c>, so
    /// requiring that distance to be at most one raw unit is exactly
    /// requiring <c>|cross| ≤ |b - a|</c>, and <c>|b - a|</c> is the segment
    /// length the table already carries.
    /// </para>
    /// <para>
    /// <b>What this test does not bind, established by breaking it.</b> It is
    /// insensitive to the segment length being wrong. Interpolation scales
    /// both components by the same <c>traveled / segmentLength</c> ratio, so a
    /// truncated length moves the sample *along* the segment without ever
    /// moving it *off* the segment; only the independent truncation of each
    /// coordinate does that. Reintroducing task 87's world-unit truncation
    /// leaves every assertion here passing. The segment length itself is
    /// pinned by
    /// <see cref="Build_DiagonalSegmentLength_IsTheRawRootNotTheScaledWorldUnitRoot"/>,
    /// and its effect on a walking operator by
    /// <c>TickPipelineTests.RunTick_UnassignedOperatorInGroupWithPublishedPath_WalksTheBentPolylineAtTheDesignedSpeed</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void SampleAt_OnEveryVertexAndInsideEverySegment_StaysOnThePolylineWithinOneRawUnit()
    {
        var polyline = MixedAnglePolyline();
        var arclength = PolylineArclength.Build(polyline);

        for (var vertexIndex = 0; vertexIndex < polyline.Length; vertexIndex++)
        {
            var atVertex = arclength.SampleAt(arclength.ArclengthAtVertex(vertexIndex));

            Assert.Equal(Raw(polyline[vertexIndex].X), atVertex.X);
            Assert.Equal(Raw(polyline[vertexIndex].Y), atVertex.Y);
        }

        var probed = 0;

        for (var segment = 0; segment + 1 < polyline.Length; segment++)
        {
            var segmentStart = arclength.ArclengthAtVertex(segment);
            var segmentLength = arclength.ArclengthAtVertex(segment + 1) - segmentStart;
            var ax = Raw(polyline[segment].X);
            var ay = Raw(polyline[segment].Y);
            var bx = Raw(polyline[segment + 1].X);
            var by = Raw(polyline[segment + 1].Y);

            // Thirty-one interior points per segment, deliberately not landing
            // on round fractions of the length, so the interpolation cannot
            // pass by only being exact at the halfway point.
            for (var step = 1; step < 32; step++)
            {
                var sample = arclength.SampleAt(segmentStart + (segmentLength * step / 32));
                var cross =
                    ((sample.X - ax) * (by - ay)) - ((sample.Y - ay) * (bx - ax));

                Assert.True(
                    Math.Abs(cross) <= segmentLength,
                    $"segment {segment} step {step}: perpendicular error exceeds one raw unit " +
                    $"(|cross| {Math.Abs(cross)} > segment length {segmentLength})");
                probed++;
            }
        }

        // The loop above must actually have run, or every assertion in it is
        // vacuous and this test would pass on an empty polyline.
        Assert.Equal(93, probed);
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

        var sample = arclength.SampleAt(Raw(50));

        Assert.Equal(Raw(50), sample.X);
        Assert.Equal(0L, sample.Y);
        Assert.Equal(Raw(100), sample.DirectionX);
        Assert.Equal(0L, sample.DirectionY);
        Assert.Equal(Raw(100), sample.DirectionLength);
    }

    [Fact]
    public void SampleAt_BeyondTotalLength_ClampsToTheFinalVertex()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        var sample = arclength.SampleAt(arclength.TotalLength + Raw(1_000));

        Assert.Equal(Raw(100), sample.X);
        Assert.Equal(Raw(100), sample.Y);
    }

    [Fact]
    public void SampleAt_BeforeStart_ClampsToTheFirstVertex()
    {
        var arclength = PolylineArclength.Build(RightAngleCorridorPolyline());

        var sample = arclength.SampleAt(Raw(-1_000));

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

        var leaderArclength = Raw(200); // the leader has walked the full corridor: it stands at (100, 100), heading south.
        var trailOffset = Raw(150); // this follower's own position is 150 units behind the leader, along the path: arclength 50, still on the east-heading leg.
        var lateralOffset = Raw(15); // half the formation width; well inside the corridor's own half-width of 20.

        // The correct, arclength-based target: the follower's own point on
        // the path (50, 0), offset perpendicular to *that point's own*
        // direction of travel (east), which is a shift in Y.
        var correctTarget = SlotTargets.ComputeTarget(path, leaderArclength, trailOffset, lateralOffset);
        Assert.Equal((Raw(50), Raw(-15)), correctTarget);

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
        Assert.Equal((Raw(115), Raw(-50)), rigidTarget);

        // A corridor of half-width 20 centred on the polyline: the union of
        // the horizontal leg's slab and the vertical leg's slab.
        static bool IsInsideCorridor(long x, long y) =>
            (x >= Raw(-20) && x <= Raw(100) && y >= Raw(-20) && y <= Raw(20)) ||
            (x >= Raw(80) && x <= Raw(120) && y >= Raw(-20) && y <= Raw(120));

        Assert.True(IsInsideCorridor(correctTarget.X, correctTarget.Y));
        Assert.False(IsInsideCorridor(rigidTarget.X, rigidTarget.Y));
    }

    [Fact]
    public void ComputeTarget_ZeroLateralOffset_LeavesTheTargetOnTheCentreline()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        var target = SlotTargets.ComputeTarget(path, Raw(200), Raw(150), lateralOffset: 0);

        Assert.Equal((Raw(50), 0L), target);
    }

    [Fact]
    public void ComputeTarget_TrailOffsetPastTheStart_ClampsRatherThanExtrapolating()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        var target = SlotTargets.ComputeTarget(path, Raw(50), Raw(10_000), lateralOffset: 0);

        Assert.Equal((0L, 0L), target);
    }

    [Fact]
    public void ComputeTarget_NegativeTrailOffset_Throws()
    {
        var path = PolylineArclength.Build(RightAngleCorridorPolyline());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlotTargets.ComputeTarget(path, Raw(100), trailOffset: -1, lateralOffset: 0));
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

        for (long trailOffset = 0; trailOffset <= Raw(200); trailOffset += Raw(17))
        {
            var first = SlotTargets.ComputeTarget(firstPath, Raw(200), trailOffset, Raw(12));
            var second = SlotTargets.ComputeTarget(secondPath, Raw(200), trailOffset, Raw(12));

            Assert.Equal(first, second);
        }
    }
}
