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
        ArgumentNullException.ThrowIfNull(wallBuckets);

        var cellBuffer = new int[grid.Width + grid.Height + 1];
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
}
