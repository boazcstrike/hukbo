using Sandata.Core.Geometry;

namespace Sandata.Core.Navigation;

/// <summary>
/// The two-phase line-of-sight query design section 7 specifies: "Phase one
/// is <c>GridRay.Traverse</c> ... which enumerates in strict order the few
/// cells whose wall buckets need checking. Phase two is
/// <c>ExactPredicates.ClassifySegments</c> against the real wall list in
/// those buckets, which answers authoritatively."
/// </summary>
/// <remarks>
/// <para>
/// Supercover rasterisation alone is insufficient, and this is why the two
/// phases stay separate rather than collapsing into "the query is blocked
/// whenever a wall shares a cell with the line". Supercover only answers
/// "which cells does this line touch", which is a proxy for "does this line
/// cross a wall", and a shallow wall angle is exactly where the two answers
/// part ways: a wall segment and the sightline can share a grid cell — both
/// pass through it — without the two segments actually crossing anywhere
/// inside that cell. <c>LineOfSightTests</c> demonstrates a concrete
/// case of that disagreement and asserts that <see cref="ExactPredicates.ClassifySegments"/>,
/// not cell membership, is what this type reports.
/// </para>
/// <para>
/// A relation of anything other than <see cref="SegmentRelation.Disjoint"/>
/// counts as blocking: a proper crossing, an endpoint touch, or a collinear
/// overlap with a wall are all cases where the sightline does not cleanly
/// pass the wall by.
/// </para>
/// <para>
/// <b>Every query needs a cell buffer, and a hot caller must own it.</b> Task
/// 88 of <c>docs/plans/2026-08-07-sandata-scaffold.md</c> measured this method
/// allocating a fresh <c>int[grid.Width + grid.Height + 1]</c> on every call
/// at roughly 4,684 calls per tick — by a wide margin the largest allocator in
/// the whole tick. The overloads taking a <see cref="Span{T}"/> exist for that
/// reason and are what <c>SandataSimulation</c>'s stage 5 uses. The overloads
/// without one still allocate, deliberately: they serve callers that run this
/// query once per published path rather than once per operator pair, and
/// giving those a buffer they would have to size and thread through buys
/// nothing measurable. <b>A new per-tick, per-pair caller belongs on the
/// buffer overload</b>, and <see cref="RequiredCellBufferLength"/> is how it
/// sizes one.
/// </para>
/// <para>
/// <b>A reused buffer cannot corrupt an answer, and the reason is phase two.</b>
/// Task 88 established this by breaking it: reading the whole buffer instead of
/// only the prefix <see cref="GridRay.Traverse"/> just wrote changes no result
/// at all. A stale cell only ever adds candidate wall segments, and
/// <see cref="ExactPredicates.ClassifySegments"/> then tests each one against
/// this query's own two endpoints — so a wall that does not actually cross the
/// sightline is rejected no matter which cell proposed it. The broad phase is
/// an optimisation over the whole wall list and never a correctness input. What
/// a stale read does cost is time, and what a genuinely out-of-range index
/// would cost is a throw from <see cref="WallBuckets.SegmentsInCell"/>, which is
/// why the length check below is an exception rather than a silent clamp.
/// </para>
/// </remarks>
public static class LineOfSight
{
    /// <summary>
    /// The record index of the first wall segment that blocks the line from
    /// <c>(originX, originY)</c> to <c>(targetX, targetY)</c>, or <c>-1</c>
    /// when nothing blocks it.
    /// </summary>
    /// <remarks>
    /// "First" means first in broad-phase cell-visitation order — the order
    /// <see cref="GridRay.Traverse"/> walks cells from the origin toward the
    /// target — and, within a single cell's bucket, first in ascending
    /// record index. Because <see cref="WallBuckets.Build"/> always inserts
    /// in ascending record index and <see cref="GridRay.Traverse"/> always
    /// walks the same two points in the same order, the same query always
    /// names the same blocking wall.
    /// </remarks>
    public static int FirstBlockingSegment(
        long originX,
        long originY,
        long targetX,
        long targetY,
        NavGrid grid,
        WallBuckets wallBuckets)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return FirstBlockingSegment(
            originX, originY, targetX, targetY, grid, wallBuckets,
            new int[RequiredCellBufferLength(grid)]);
    }

    /// <summary>
    /// The length a caller-supplied <c>cellBuffer</c> must have for
    /// <paramref name="grid"/>: the longest cell chain
    /// <see cref="GridRay.Traverse"/> can produce across it, which is one step
    /// per column plus one per row plus the origin cell itself.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    public static int RequiredCellBufferLength(NavGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return grid.Width + grid.Height + 1;
    }

    /// <summary>
    /// <see cref="FirstBlockingSegment(long, long, long, long, NavGrid, WallBuckets)"/>
    /// against a scratch buffer the caller owns, for a caller that runs this
    /// query many times per tick.
    /// </summary>
    /// <param name="cellBuffer">
    /// Scratch space for <see cref="GridRay.Traverse"/>'s cell chain, at least
    /// <see cref="RequiredCellBufferLength"/> long. Written on every call and
    /// read only within it, and this method stores no reference to it, so one
    /// buffer serves every query against <paramref name="grid"/>. See this
    /// type's remarks for why a stale entry could not change an answer even if
    /// one were read.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="cellBuffer"/> is shorter than <see cref="RequiredCellBufferLength"/>.</exception>
    public static int FirstBlockingSegment(
        long originX,
        long originY,
        long targetX,
        long targetY,
        NavGrid grid,
        WallBuckets wallBuckets,
        Span<int> cellBuffer)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        var required = RequiredCellBufferLength(grid);
        if (cellBuffer.Length < required)
        {
            throw new ArgumentException(
                $"cellBuffer must hold at least {required} cells for a {grid.Width}x{grid.Height} grid.",
                nameof(cellBuffer));
        }

        var cellCount = GridRay.Traverse(originX, originY, targetX, targetY, cellBuffer, grid);

        for (var cellPosition = 0; cellPosition < cellCount; cellPosition++)
        {
            var candidates = wallBuckets.SegmentsInCell(cellBuffer[cellPosition]);

            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var segmentIndex = candidates[candidateIndex];

                var relation = ExactPredicates.ClassifySegments(
                    originX, originY, targetX, targetY,
                    wallBuckets.SegmentAX(segmentIndex), wallBuckets.SegmentAY(segmentIndex),
                    wallBuckets.SegmentBX(segmentIndex), wallBuckets.SegmentBY(segmentIndex));

                if (relation != SegmentRelation.Disjoint)
                {
                    return segmentIndex;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether the line from <c>(originX, originY)</c> to
    /// <c>(targetX, targetY)</c> reaches its target with no wall blocking
    /// it. Equivalent to <see cref="FirstBlockingSegment"/> returning
    /// <c>-1</c>.
    /// </summary>
    public static bool IsVisible(
        long originX,
        long originY,
        long targetX,
        long targetY,
        NavGrid grid,
        WallBuckets wallBuckets) =>
        FirstBlockingSegment(originX, originY, targetX, targetY, grid, wallBuckets) < 0;

    /// <summary>
    /// <see cref="IsVisible(long, long, long, long, NavGrid, WallBuckets)"/>
    /// against a caller-owned scratch buffer — see the
    /// <see cref="FirstBlockingSegment(long, long, long, long, NavGrid, WallBuckets, Span{int})"/>
    /// overload this delegates to for the buffer's contract.
    /// </summary>
    public static bool IsVisible(
        long originX,
        long originY,
        long targetX,
        long targetY,
        NavGrid grid,
        WallBuckets wallBuckets,
        Span<int> cellBuffer) =>
        FirstBlockingSegment(originX, originY, targetX, targetY, grid, wallBuckets, cellBuffer) < 0;
}
