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

        var cellCount = grid.CellCount;
        var countPerCell = new int[cellCount];
        var traversalBuffer = new int[grid.Width + grid.Height + 1];

        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            var touchedCount = GridRay.Traverse(
                segmentAX[segmentIndex], segmentAY[segmentIndex],
                segmentBX[segmentIndex], segmentBY[segmentIndex],
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
                segmentAX[segmentIndex], segmentAY[segmentIndex],
                segmentBX[segmentIndex], segmentBY[segmentIndex],
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
}
