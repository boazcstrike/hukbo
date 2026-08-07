using Sandata.Core.Geometry;
using Sandata.Core.Navigation;

namespace Sandata.Core.Orders;

/// <summary>
/// Design section 16's four <see cref="OrderKind.MoveAlongPath"/> rejection
/// rules, checked as one unit at submission time — "An order is validated
/// when it is submitted, not when it is applied." This type performs no
/// mutation and holds no state; every method is a pure function of its
/// arguments, so it can run identically on any machine given the same map
/// bake, matching the rest of <c>Sandata.Core</c>'s determinism contract.
/// </summary>
public static class OrderValidation
{
    /// <summary>
    /// Checks <paramref name="path"/> against design section 16's four
    /// rejection rules, in this order: node count, map bounds, blocked
    /// cells, wall crossings. The first rule violated is the one reported;
    /// this method does not collect every violation a malformed path
    /// carries; the design does not ask for that, and the plan's task 57 row
    /// treats a rejection as a single reason code, not a list.
    /// </summary>
    /// <param name="path">The candidate authored polyline, verbatim.</param>
    /// <param name="grid">
    /// The current nav bake. <see cref="NavGrid.Passability"/> supplies the
    /// blocked-cell rule; <see cref="NavGrid.Width"/> and
    /// <see cref="NavGrid.Height"/>, scaled by <see cref="NavGrid.CellSizeWu"/>,
    /// supply the map-bounds rule, since <see cref="NavGrid"/> itself has no
    /// world-unit width or height property of its own.
    /// </param>
    /// <param name="wallBuckets">
    /// The wall bucket index the segment-crossing rule's broad phase queries,
    /// built over the same map <paramref name="grid"/> was baked from.
    /// </param>
    /// <returns>
    /// <see langword="null"/> if <paramref name="path"/> passes every rule;
    /// otherwise the <see cref="OrderRejectReason"/> for the first rule it
    /// fails.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="grid"/> or <paramref name="wallBuckets"/> is <see langword="null"/>.
    /// </exception>
    public static OrderRejectReason? ValidateMoveAlongPath(AuthoredPath path, NavGrid grid, WallBuckets wallBuckets)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        if (path.NodeCount < 2 || path.NodeCount > Order.MaxAuthoredPathNodeCount)
        {
            return OrderRejectReason.InvalidNodeCount;
        }

        var widthWu = (long)grid.Width * NavGrid.CellSizeWu;
        var heightWu = (long)grid.Height * NavGrid.CellSizeWu;

        foreach (var node in path.Nodes)
        {
            if (IsOutsideMapBounds(node, widthWu, heightWu))
            {
                return OrderRejectReason.NodeOutsideMapBounds;
            }
        }

        foreach (var node in path.Nodes)
        {
            if (IsInBlockedCell(node, grid))
            {
                return OrderRejectReason.NodeInBlockedCell;
            }
        }

        foreach (var (from, to) in path.Segments())
        {
            if (SegmentCrossesAnyWall(from, to, grid, wallBuckets))
            {
                return OrderRejectReason.SegmentCrossesWall;
            }
        }

        return null;
    }

    /// <summary>
    /// Design section 16's first rule: "A node lies outside the map bounds."
    /// The closed interval <c>[0, widthWu] x [0, heightWu]</c> — the same
    /// closed interval <c>MapTokenizer</c> validates a <c>WALL</c>/<c>DOOR</c>
    /// endpoint against, so a node sitting exactly on the map's far edge is
    /// in bounds, not rejected.
    /// </summary>
    private static bool IsOutsideMapBounds(OrderPathNode node, long widthWu, long heightWu) =>
        node.X < 0 || node.X > widthWu || node.Y < 0 || node.Y > heightWu;

    /// <summary>
    /// Design section 16's second rule: "A node lies in a cell that is
    /// <c>Blocked</c> in the current nav bake." A node exactly on the map's
    /// far edge converts to a cell coordinate one past the grid's last
    /// column or row — <c>NavBake.RasterizeSegment</c>'s own documented
    /// "zero-width outer boundary" case, "not a real cell to rasterize." The
    /// same convention applies here: that coordinate is in bounds by
    /// <see cref="IsOutsideMapBounds"/>, but it names no real cell, so it
    /// cannot be a <em>blocked</em> one either.
    /// </summary>
    private static bool IsInBlockedCell(OrderPathNode node, NavGrid grid)
    {
        var cellX = NavGrid.WorldToCellCoordinate(node.X);
        var cellY = NavGrid.WorldToCellCoordinate(node.Y);

        if (!grid.TryGetCellIndex(cellX, cellY, out var cellIndex))
        {
            return false;
        }

        return grid.Passability[cellIndex] == NavCellFlags.Blocked;
    }

    /// <summary>
    /// Design section 16's third rule: "A segment between consecutive nodes
    /// crosses a wall segment, tested with <c>ExactPredicates.ClassifySegments</c>
    /// against the wall bucket index. This is an exact integer test with no
    /// epsilon, the same predicate line of sight uses." A relation of
    /// anything other than <see cref="SegmentRelation.Disjoint"/> counts as
    /// crossing — <c>LineOfSight.FirstBlockingSegment</c>'s own convention,
    /// reproduced here rather than called directly because that method
    /// throws <see cref="ArgumentOutOfRangeException"/> for an origin cell
    /// outside the grid, a case a node exactly on the map's far edge can
    /// legitimately reach.
    /// </summary>
    /// <remarks>
    /// The broad-phase cell walk feeds <see cref="GridRay.Traverse"/> a
    /// coordinate pair clamped to the grid's interior, so a boundary node
    /// never throws; the narrow-phase exact test always receives
    /// <paramref name="from"/> and <paramref name="to"/> unclamped, so the
    /// classification itself is never approximated. A degenerate segment
    /// (identical consecutive nodes) is skipped rather than tested —
    /// <c>ExactPredicates.ClassifySegments</c> throws
    /// <see cref="ArgumentException"/> for a zero-length input, and a
    /// duplicate node draws no segment for a wall to cross in the first
    /// place.
    /// </remarks>
    private static bool SegmentCrossesAnyWall(OrderPathNode from, OrderPathNode to, NavGrid grid, WallBuckets wallBuckets)
    {
        if (from.X == to.X && from.Y == to.Y)
        {
            return false;
        }

        var cellBuffer = new int[grid.Width + grid.Height + 1];
        var broadFromX = ClampToInterior(from.X, grid.Width);
        var broadFromY = ClampToInterior(from.Y, grid.Height);
        var broadToX = ClampToInterior(to.X, grid.Width);
        var broadToY = ClampToInterior(to.Y, grid.Height);

        var cellCount = GridRay.Traverse(broadFromX, broadFromY, broadToX, broadToY, cellBuffer, grid);

        for (var cellPosition = 0; cellPosition < cellCount; cellPosition++)
        {
            var candidates = wallBuckets.SegmentsInCell(cellBuffer[cellPosition]);
            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var segmentIndex = candidates[candidateIndex];
                var relation = ExactPredicates.ClassifySegments(
                    from.X, from.Y, to.X, to.Y,
                    wallBuckets.SegmentAX(segmentIndex), wallBuckets.SegmentAY(segmentIndex),
                    wallBuckets.SegmentBX(segmentIndex), wallBuckets.SegmentBY(segmentIndex));

                if (relation != SegmentRelation.Disjoint)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Clamps a world-unit coordinate so its cell falls inside
    /// <c>[0, cellsAlongAxis)</c>, for feeding the broad-phase cell walk
    /// alone — <see cref="GridRay.Traverse"/> throws for an origin cell
    /// outside the grid, which a coordinate exactly on the map's far edge
    /// (a legitimately in-bounds world-unit value) would otherwise trigger.
    /// The narrow-phase exact test never sees a clamped value.
    /// </summary>
    private static long ClampToInterior(long worldUnits, int cellsAlongAxis)
    {
        var maxWorldUnits = ((long)cellsAlongAxis * NavGrid.CellSizeWu) - 1;
        if (worldUnits < 0)
        {
            return 0;
        }

        return worldUnits > maxWorldUnits ? maxWorldUnits : worldUnits;
    }
}
