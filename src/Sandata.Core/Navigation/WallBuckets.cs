namespace Sandata.Core.Navigation;

/// <summary>
/// A compressed-sparse-row index from grid cell to the wall segments whose
/// broad-phase traversal touches that cell — the narrow phase's candidate
/// list for <see cref="LineOfSight"/>. Design section 7: "wallBucketStart,
/// wallBucketItems — int[] — a compressed-sparse-row index from cell to
/// wall segment — built at load, immutable."
/// </summary>
/// <remarks>
/// <para>
/// Every wall segment is rasterised into its candidate cells with the same
/// <see cref="GridRay.Traverse"/> broad phase that <see cref="LineOfSight"/>
/// later uses to enumerate a query's own cells, so both traversals agree by
/// construction on what "touches this cell" means: a cell holds a segment
/// exactly when a straight line between that segment's two endpoints would
/// pass through it. That agreement is what makes a wall bucket a safe
/// broad-phase candidate list — it can only over-approximate a true
/// crossing, never miss one, and the narrow phase in
/// <see cref="LineOfSight"/> resolves every over-approximation exactly.
/// </para>
/// <para>
/// Within one cell's bucket, segments are stored in ascending record index
/// — the order they were passed to <see cref="Build"/> — because
/// <see cref="Build"/> processes segments strictly in that order and only
/// ever appends. That ordering is load-bearing: it is what lets
/// <see cref="LineOfSight.FirstBlockingSegment"/> report the same blocking
/// wall every time the same query runs, per plan task 20's test bar.
/// </para>
/// </remarks>
public sealed class WallBuckets
{
    private readonly int[] bucketStart;
    private readonly int[] bucketItems;
    private readonly long[] segmentAX;
    private readonly long[] segmentAY;
    private readonly long[] segmentBX;
    private readonly long[] segmentBY;

    private WallBuckets(
        int[] bucketStart,
        int[] bucketItems,
        long[] segmentAX,
        long[] segmentAY,
        long[] segmentBX,
        long[] segmentBY)
    {
        this.bucketStart = bucketStart;
        this.bucketItems = bucketItems;
        this.segmentAX = segmentAX;
        this.segmentAY = segmentAY;
        this.segmentBX = segmentBX;
        this.segmentBY = segmentBY;
    }

    /// <summary>The number of wall segments this index was built from.</summary>
    public int SegmentCount => segmentAX.Length;

    /// <summary>The A endpoint's X coordinate of wall segment <paramref name="segmentIndex"/>.</summary>
    public long SegmentAX(int segmentIndex) => segmentAX[segmentIndex];

    /// <summary>The A endpoint's Y coordinate of wall segment <paramref name="segmentIndex"/>.</summary>
    public long SegmentAY(int segmentIndex) => segmentAY[segmentIndex];

    /// <summary>The B endpoint's X coordinate of wall segment <paramref name="segmentIndex"/>.</summary>
    public long SegmentBX(int segmentIndex) => segmentBX[segmentIndex];

    /// <summary>The B endpoint's Y coordinate of wall segment <paramref name="segmentIndex"/>.</summary>
    public long SegmentBY(int segmentIndex) => segmentBY[segmentIndex];

    /// <summary>
    /// The record indices of every wall segment whose broad-phase
    /// traversal touched <paramref name="cellIndex"/>, in ascending record
    /// index. Empty when no segment touches that cell.
    /// </summary>
    public ReadOnlySpan<int> SegmentsInCell(int cellIndex)
    {
        var start = bucketStart[cellIndex];
        var end = bucketStart[cellIndex + 1];
        return bucketItems.AsSpan(start, end - start);
    }

    /// <summary>
    /// Builds the index over <paramref name="grid"/> from the wall segments
    /// named by the four parallel coordinate spans, whose shared index is
    /// the segment's record index. Two passes over
    /// <see cref="GridRay.Traverse"/>: the first counts how many segments
    /// touch each cell so the compressed-sparse-row offsets can be computed
    /// by a running prefix sum with no dictionary or growable list, and the
    /// second walks every segment again in the same ascending order to fill
    /// each cell's slice, so no bucket ever needs sorting after the fact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Boundary walls.</b> Design section 12 validates a <c>WALL</c>
    /// endpoint against the closed interval <c>[0, widthWu] x [0, heightWu]</c>,
    /// so an outer-shell wall's endpoint legitimately sits exactly on the
    /// map's far edge — <c>angle-house.hkmap</c>'s four perimeter walls all
    /// do. That world-unit value floors to the cell one past the grid's last
    /// column or row (<see cref="NavGrid.WorldToCellCoordinate"/>), which
    /// <see cref="GridRay.Traverse"/> rejects as out of bounds by design (it
    /// must, so a genuinely stray origin is still caught). Every endpoint
    /// this method feeds to <see cref="GridRay.Traverse"/> is therefore
    /// first clamped into the grid's interior by <see cref="ClampToInterior"/>
    /// — the same broad-phase-only clamp <see cref="Sandata.Core.Orders.OrderValidation.ValidateMoveAlongPath"/>
    /// already applies to an authored path node for the identical reason.
    /// The clamp never touches <see cref="segmentAX"/>/<see cref="segmentAY"/>/
    /// <see cref="segmentBX"/>/<see cref="segmentBY"/> below, which store the
    /// segment's true, unclamped coordinates exactly as passed in — those are
    /// what <see cref="LineOfSight.FirstBlockingSegment"/> later feeds
    /// <see cref="Geometry.ExactPredicates.ClassifySegments"/>, and that exact
    /// narrow phase must never see approximated geometry.
    /// </para>
    /// <para>
    /// A segment whose <em>both</em> endpoints lie outside the closed
    /// interval above is not a boundary wall grazing the edge — it is
    /// geometry that does not belong to this grid at all, and clamping it
    /// would silently invent a false touch at whichever interior cell the
    /// clamp happened to land on. <see cref="Build"/> rejects that case with
    /// an <see cref="ArgumentException"/> naming the offending record index
    /// rather than bucketing a wall that never actually reaches the grid.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A segment's coordinate spans disagree in length, or a segment's two
    /// endpoints both lie outside <paramref name="grid"/>'s world-unit bounds.
    /// </exception>
    public static WallBuckets Build(
        NavGrid grid,
        ReadOnlySpan<long> segmentAX,
        ReadOnlySpan<long> segmentAY,
        ReadOnlySpan<long> segmentBX,
        ReadOnlySpan<long> segmentBY)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var segmentCount = segmentAX.Length;
        if (segmentAY.Length != segmentCount || segmentBX.Length != segmentCount || segmentBY.Length != segmentCount)
        {
            throw new ArgumentException("All four segment coordinate spans must have the same length.", nameof(segmentAY));
        }

        var widthWu = (long)grid.Width * NavGrid.CellSizeWu;
        var heightWu = (long)grid.Height * NavGrid.CellSizeWu;

        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            if (IsOutsideGridBounds(segmentAX[segmentIndex], segmentAY[segmentIndex], widthWu, heightWu) &&
                IsOutsideGridBounds(segmentBX[segmentIndex], segmentBY[segmentIndex], widthWu, heightWu))
            {
                throw new ArgumentException(
                    $"Wall segment {segmentIndex} has both endpoints outside the grid's " +
                    $"[0, {widthWu}] x [0, {heightWu}] world-unit bounds, so it cannot touch " +
                    "any cell this index could correctly bucket it into.",
                    nameof(segmentAX));
            }
        }

        var cellCount = grid.CellCount;
        var countPerCell = new int[cellCount];
        var traversalBuffer = new int[grid.Width + grid.Height + 1];

        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            var touchedCount = GridRay.Traverse(
                ClampToInterior(segmentAX[segmentIndex], grid.Width), ClampToInterior(segmentAY[segmentIndex], grid.Height),
                ClampToInterior(segmentBX[segmentIndex], grid.Width), ClampToInterior(segmentBY[segmentIndex], grid.Height),
                traversalBuffer, grid);

            for (var touchedIndex = 0; touchedIndex < touchedCount; touchedIndex++)
            {
                countPerCell[traversalBuffer[touchedIndex]]++;
            }
        }

        var bucketStart = new int[cellCount + 1];
        var running = 0;
        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            bucketStart[cellIndex] = running;
            running += countPerCell[cellIndex];
        }

        bucketStart[cellCount] = running;

        var bucketItems = new int[running];
        var cursor = (int[])bucketStart.Clone();

        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            var touchedCount = GridRay.Traverse(
                ClampToInterior(segmentAX[segmentIndex], grid.Width), ClampToInterior(segmentAY[segmentIndex], grid.Height),
                ClampToInterior(segmentBX[segmentIndex], grid.Width), ClampToInterior(segmentBY[segmentIndex], grid.Height),
                traversalBuffer, grid);

            for (var touchedIndex = 0; touchedIndex < touchedCount; touchedIndex++)
            {
                var cellIndex = traversalBuffer[touchedIndex];
                bucketItems[cursor[cellIndex]] = segmentIndex;
                cursor[cellIndex]++;
            }
        }

        return new WallBuckets(
            bucketStart,
            bucketItems,
            segmentAX.ToArray(),
            segmentAY.ToArray(),
            segmentBX.ToArray(),
            segmentBY.ToArray());
    }

    /// <summary>
    /// Whether world-unit point (<paramref name="x"/>, <paramref name="y"/>)
    /// lies outside the closed interval <c>[0, widthWu] x [0, heightWu]</c> —
    /// the same closed-interval rule design section 12 validates a
    /// <c>WALL</c> endpoint against, and the same predicate
    /// <see cref="Sandata.Core.Orders.OrderValidation"/> applies to an authored path node.
    /// A point sitting exactly on the map's far edge is therefore in bounds,
    /// not outside.
    /// </summary>
    private static bool IsOutsideGridBounds(long x, long y, long widthWu, long heightWu) =>
        x < 0 || x > widthWu || y < 0 || y > heightWu;

    /// <summary>
    /// Clamps a world-unit coordinate so its cell falls inside
    /// <c>[0, cellsAlongAxis)</c>, for feeding the broad-phase
    /// <see cref="GridRay.Traverse"/> walk alone — that method throws for an
    /// origin cell outside the grid, which a coordinate exactly on the map's
    /// far edge (a legitimately in-bounds world-unit value) would otherwise
    /// trigger. <see cref="Build"/> never stores this clamped value; the
    /// narrow-phase exact test always reads the true coordinate back from
    /// <see cref="segmentAX"/>/<see cref="segmentAY"/>/<see cref="segmentBX"/>/
    /// <see cref="segmentBY"/> instead. Matches
    /// <see cref="Sandata.Core.Orders.OrderValidation"/>'s own <c>ClampToInterior</c>
    /// helper, established there first for the identical broad-phase-only
    /// purpose.
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
