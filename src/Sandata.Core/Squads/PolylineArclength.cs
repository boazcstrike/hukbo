using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Sandata.Core.Navigation;

namespace Sandata.Core.Squads;

/// <summary>
/// One sample taken from a <see cref="PolylineArclength"/> at a given
/// arclength: the interpolated world-unit point, and the direction and
/// Euclidean length of the polyline segment that point was interpolated
/// from. <see cref="DirectionX"/> and <see cref="DirectionY"/> are the raw,
/// un-normalised segment delta — not a unit vector — because
/// <see cref="SlotTargets.ComputeTarget"/> divides by <see cref="DirectionLength"/>
/// itself when it needs a unit perpendicular, and carrying the un-normalised
/// pair avoids losing precision to an intermediate rounding step nobody
/// asked for. <see cref="DirectionLength"/> is zero exactly when the sampled
/// point falls on (or the whole polyline collapses to) a single vertex with
/// no defined direction of travel — a duplicate consecutive vertex, or a
/// one-vertex polyline.
/// </summary>
/// <param name="X">The sampled point's X coordinate, in world units.</param>
/// <param name="Y">The sampled point's Y coordinate, in world units.</param>
/// <param name="DirectionX">The source segment's raw, un-normalised X delta, or zero when no direction is defined at this sample.</param>
/// <param name="DirectionY">The source segment's raw, un-normalised Y delta, or zero when no direction is defined at this sample.</param>
/// <param name="DirectionLength">
/// The source segment's true Euclidean length — see <see cref="PolylineArclength"/>'s
/// remarks for why this is Euclidean rather than a squared-free measure —
/// or zero when no direction is defined at this sample.
/// </param>
public readonly record struct ArclengthSample(
    long X,
    long Y,
    long DirectionX,
    long DirectionY,
    long DirectionLength);

/// <summary>
/// Cumulative integer arclength precomputed once over a shared group
/// polyline — design section 8 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Arclength slot
/// offsets": "The shared search result is a polyline carrying precomputed
/// cumulative integer arclength at each vertex." <see cref="SlotTargets"/>
/// consumes an instance of this type to turn a slot's trail offset and
/// lateral offset into a world-unit target, without ever needing its own
/// notion of the leader's heading.
/// </summary>
/// <remarks>
/// <para>
/// <b>Arclength measure: true Euclidean segment length, computed by exact
/// integer square root, not a squared-free approximation.</b> A squared-free
/// measure (Manhattan or the octile "10/14" measure the nav heuristic and
/// clearance field use) is only faithful to true distance along the
/// specific directions it was built for — grid-axis and exact 45-degree
/// diagonal moves. The polylines this type measures are the funnel- and
/// line-of-sight-smoothed paths <c>PathService.GetCurrentPath</c> publishes,
/// whose segments can sit at an arbitrary angle — the fixture's own
/// 18.4-degree wall is the standing example (design section 6). Two
/// consumers need the true distance, not an approximation of it: the
/// cumulative sum has to represent the same "how far along the path" a
/// human would measure with a tape, so a follower's trail offset places it
/// the right physical distance behind the leader regardless of the angle of
/// the ground it is crossing; and <see cref="SlotTargets.ComputeTarget"/>
/// divides a lateral offset by <see cref="ArclengthSample.DirectionLength"/>
/// to build a unit perpendicular — dividing by an approximate length would
/// scale that perpendicular by the wrong amount on every non-axis-aligned
/// segment, which is precisely the class of segment this type exists to
/// handle correctly. This is computed with
/// <see cref="FixedPoint.IntegerSquareRoot"/>, the same exact bitwise
/// restoring square root the collision code
/// (<c>SandataCollisionGrid</c>) and Hukbo's own battle simulation already
/// rely on for hashed, replay-critical distances: it is a closed-form
/// integer algorithm with no floating-point rounding and no
/// platform-dependent instruction sequence, so the same input pair of
/// vertices always produces the same cumulative arclength, everywhere and
/// forever.
/// </para>
/// <para>
/// <b>Deterministic across two evaluations.</b> <see cref="Build"/> reads
/// only <paramref name="polyline"/>, and every step from there is pure
/// integer arithmetic with no shared or timing-dependent state, so calling
/// it twice with the same polyline value produces two instances whose every
/// cumulative entry is bit-identical — exactly what
/// <c>SlotTargetsTests</c> pins.
/// </para>
/// </remarks>
public readonly struct PolylineArclength
{
    private readonly ImmutableArray<PathPoint> _vertices;
    private readonly ImmutableArray<long> _cumulativeLength;

    private PolylineArclength(ImmutableArray<PathPoint> vertices, ImmutableArray<long> cumulativeLength)
    {
        _vertices = vertices;
        _cumulativeLength = cumulativeLength;
    }

    /// <summary>
    /// Precomputes cumulative arclength over <paramref name="polyline"/>,
    /// start-to-finish, vertex by vertex.
    /// </summary>
    /// <param name="polyline">
    /// The shared group polyline, start point first — the shape
    /// <c>PathService.GetCurrentPath</c> publishes. Must hold at least one
    /// vertex.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="polyline"/> is a default (uninitialised)
    /// <see cref="ImmutableArray{T}"/>, or holds no vertex.
    /// </exception>
    public static PolylineArclength Build(ImmutableArray<PathPoint> polyline)
    {
        if (polyline.IsDefault)
        {
            throw new ArgumentException(
                "polyline must not be a default ImmutableArray.", nameof(polyline));
        }

        if (polyline.Length == 0)
        {
            throw new ArgumentException(
                "polyline must hold at least one vertex.", nameof(polyline));
        }

        var cumulativeLength = ImmutableArray.CreateBuilder<long>(polyline.Length);
        cumulativeLength.Add(0);

        for (var index = 1; index < polyline.Length; index++)
        {
            var previous = polyline[index - 1];
            var current = polyline[index];
            var deltaX = current.X - previous.X;
            var deltaY = current.Y - previous.Y;
            var segmentLengthSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
            var segmentLength = FixedPoint.IntegerSquareRoot(segmentLengthSquared);
            cumulativeLength.Add(checked(cumulativeLength[index - 1] + segmentLength));
        }

        return new PolylineArclength(polyline, cumulativeLength.MoveToImmutable());
    }

    /// <summary>The number of vertices <see cref="Build"/> was given.</summary>
    public int VertexCount => _vertices.Length;

    /// <summary>The polyline's total arclength: the last entry of the cumulative table, and never negative.</summary>
    public long TotalLength => _cumulativeLength[^1];

    /// <summary>The precomputed cumulative arclength at <paramref name="vertexIndex"/>, matching the vertex <see cref="Build"/> was given at that index.</summary>
    public long ArclengthAtVertex(int vertexIndex) => _cumulativeLength[vertexIndex];

    /// <summary>
    /// Binary search into the precomputed cumulative arclength table for the
    /// greatest vertex index whose cumulative arclength does not exceed
    /// <paramref name="arclength"/>, clamped to <c>[0, VertexCount - 1]</c>
    /// for a query outside the polyline's own range. A query that lands
    /// exactly on a vertex's own cumulative arclength returns that vertex's
    /// index exactly — the table is non-decreasing, never strictly
    /// increasing (a zero-length segment repeats a cumulative value), so
    /// this search always resolves ties toward the higher index, which is
    /// what makes "the exact vertex" well defined rather than ambiguous.
    /// </summary>
    public int FloorVertexIndex(long arclength)
    {
        var lastIndex = _cumulativeLength.Length - 1;

        if (arclength <= _cumulativeLength[0])
        {
            return 0;
        }

        if (arclength >= _cumulativeLength[lastIndex])
        {
            return lastIndex;
        }

        var low = 0;
        var high = lastIndex;

        while (low < high)
        {
            var mid = low + ((high - low + 1) / 2);

            if (_cumulativeLength[mid] <= arclength)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// Samples the polyline at <paramref name="arclength"/>, clamped to
    /// <c>[0, TotalLength]</c> so a caller's out-of-range trail offset holds
    /// at the path's start rather than throwing or extrapolating past it.
    /// The returned point is linearly interpolated within the segment
    /// <see cref="FloorVertexIndex"/> selects; the returned direction is
    /// that segment's own raw delta and true Euclidean length, except at the
    /// polyline's final vertex, where there is no outgoing segment and the
    /// last incoming segment's direction is reported instead. A one-vertex
    /// polyline, or a sample landing on a zero-length (duplicate-vertex)
    /// segment, reports a zero direction and a zero length — see
    /// <see cref="ArclengthSample"/>.
    /// </summary>
    public ArclengthSample SampleAt(long arclength)
    {
        var clamped = arclength;

        if (clamped < 0)
        {
            clamped = 0;
        }

        var total = TotalLength;

        if (clamped > total)
        {
            clamped = total;
        }

        var lastVertexIndex = _vertices.Length - 1;

        if (lastVertexIndex == 0)
        {
            var only = _vertices[0];
            return new ArclengthSample(only.X, only.Y, 0, 0, 0);
        }

        var floorIndex = FloorVertexIndex(clamped);
        var segmentStartIndex = floorIndex == lastVertexIndex ? floorIndex - 1 : floorIndex;

        var start = _vertices[segmentStartIndex];
        var end = _vertices[segmentStartIndex + 1];
        var directionX = end.X - start.X;
        var directionY = end.Y - start.Y;
        var segmentLength = _cumulativeLength[segmentStartIndex + 1] - _cumulativeLength[segmentStartIndex];

        if (segmentLength == 0)
        {
            return new ArclengthSample(start.X, start.Y, 0, 0, 0);
        }

        var traveled = clamped - _cumulativeLength[segmentStartIndex];
        var x = checked(start.X + (directionX * traveled / segmentLength));
        var y = checked(start.Y + (directionY * traveled / segmentLength));

        return new ArclengthSample(x, y, directionX, directionY, segmentLength);
    }
}
