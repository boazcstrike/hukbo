namespace Sandata.Core.Navigation;

/// <summary>
/// Division-free integer Amanatides-Woo grid traversal: enumerates, in
/// strict travel order, every cell a straight line between two world-space
/// points passes through. Design section 7, "Line of sight, two phase":
/// this is the broad phase alone — its only job is choosing which cells'
/// wall buckets need checking, and it never itself decides whether
/// anything is occluded. <see cref="LineOfSight"/> is what pairs this with
/// the narrow phase to answer that question.
/// </summary>
/// <remarks>
/// <para>
/// The textbook Amanatides-Woo walk accumulates a running parametric value
/// <c>tMax</c> per axis in floating point, comparing <c>tMaxX</c> against
/// <c>tMaxY</c> to decide which axis's grid line the ray crosses next. Both
/// <c>float</c> and division of a parametric value are banned in this
/// project, so this type keeps every such value as a rational pair instead.
/// </para>
/// <para>
/// For one axis, every <c>tMax</c> the walk ever needs shares the same
/// denominator: the absolute value of that axis's total displacement. Only
/// the numerator changes, growing by exactly one cell width every time that
/// axis is stepped, because each step advances to the next grid line, which
/// is one cell width further along. That means the whole walk needs no
/// division at all: the numerator starts at the world-unit distance from
/// the origin to the first grid line ahead of it and is incremented by the
/// cell size on every step of that axis, while the two axes are compared to
/// each other by cross-multiplying each numerator against the other axis's
/// denominator — <c>numeratorX * absDeltaY</c> versus
/// <c>numeratorY * absDeltaX</c> — which preserves the ordering of the two
/// fractions exactly because both denominators are non-negative by
/// construction (they are absolute values).
/// </para>
/// <para>
/// <b>The diagonal-corner tie.</b> When a line passes through the exact
/// corner shared by four cells, it crosses a vertical grid line and a
/// horizontal grid line at precisely the same parameter, so the two
/// cross-multiplied products above compare equal. The written rule this
/// type resolves that tie by is: step X first. Concretely, on a tie this
/// walk advances the X axis alone on that iteration; the very next
/// comparison is then no longer a tie, because the X numerator has grown by
/// one cell width while the Y numerator has not, so the following iteration
/// always advances Y. A line running exactly along the diagonal therefore
/// visits cells in an alternating X-then-Y staircase rather than jumping to
/// the diagonal neighbour and silently skipping the two cells that share an
/// edge with it — a corner is never crossed without touching both cells
/// that border it.
/// </para>
/// </remarks>
public static class GridRay
{
    /// <summary>
    /// Walks the grid from <c>(originX, originY)</c> to
    /// <c>(targetX, targetY)</c> — both expressed in whole world units — and
    /// writes the flat cell index of every cell the line passes through,
    /// starting with the origin's own cell and ending with the target's own
    /// cell (both endpoints are inclusive), into <paramref name="cells"/> in
    /// strict travel order.
    /// </summary>
    /// <param name="originX">The origin point's X coordinate, in world units.</param>
    /// <param name="originY">The origin point's Y coordinate, in world units.</param>
    /// <param name="targetX">The target point's X coordinate, in world units.</param>
    /// <param name="targetY">The target point's Y coordinate, in world units.</param>
    /// <param name="cells">
    /// Receives the visited cell indices in travel order. If the line would
    /// visit more cells than this span holds, or if it leaves the grid's
    /// bounds before reaching the target cell, the walk stops early and
    /// returns the number of cells actually written; it never writes past
    /// the end of <paramref name="cells"/> and it never reports a cell
    /// outside <paramref name="grid"/>'s bounds.
    /// </param>
    /// <param name="grid">The grid supplying bounds and the cell index formula.</param>
    /// <returns>The number of cell indices written into <paramref name="cells"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="cells"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The origin's cell lies outside <paramref name="grid"/>'s bounds.
    /// </exception>
    public static int Traverse(
        long originX,
        long originY,
        long targetX,
        long targetY,
        Span<int> cells,
        NavGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (cells.Length == 0)
        {
            throw new ArgumentException("The destination span must hold at least one cell.", nameof(cells));
        }

        var cellX = NavGrid.WorldToCellCoordinate(originX);
        var cellY = NavGrid.WorldToCellCoordinate(originY);

        if (!grid.IsInBounds(cellX, cellY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(originX),
                (originX, originY),
                "The origin point's cell lies outside the grid.");
        }

        var endCellX = NavGrid.WorldToCellCoordinate(targetX);
        var endCellY = NavGrid.WorldToCellCoordinate(targetY);

        var count = 0;
        cells[count++] = grid.CellIndex(cellX, cellY);

        if (cellX == endCellX && cellY == endCellY)
        {
            return count;
        }

        var deltaX = targetX - originX;
        var deltaY = targetY - originY;

        var stepX = deltaX > 0 ? 1 : deltaX < 0 ? -1 : 0;
        var stepY = deltaY > 0 ? 1 : deltaY < 0 ? -1 : 0;

        var absDeltaX = stepX >= 0 ? deltaX : -deltaX;
        var absDeltaY = stepY >= 0 ? deltaY : -deltaY;

        var xActive = stepX != 0;
        var yActive = stepY != 0;

        var numeratorX = xActive ? DistanceToNextBoundary(originX, cellX, stepX) : 0L;
        var numeratorY = yActive ? DistanceToNextBoundary(originY, cellY, stepY) : 0L;

        while (cellX != endCellX || cellY != endCellY)
        {
            bool advanceX;
            if (!xActive)
            {
                advanceX = false;
            }
            else if (!yActive)
            {
                advanceX = true;
            }
            else
            {
                var scaledX = numeratorX * absDeltaY;
                var scaledY = numeratorY * absDeltaX;

                // Equal is the diagonal-corner tie: step X first, per this
                // type's written rule (see class remarks).
                advanceX = scaledX <= scaledY;
            }

            if (advanceX)
            {
                cellX += stepX;
                numeratorX += NavGrid.CellSizeWu;
            }
            else
            {
                cellY += stepY;
                numeratorY += NavGrid.CellSizeWu;
            }

            if (!grid.IsInBounds(cellX, cellY))
            {
                break;
            }

            if (count >= cells.Length)
            {
                break;
            }

            cells[count++] = grid.CellIndex(cellX, cellY);
        }

        return count;
    }

    /// <summary>
    /// The world-unit distance from <paramref name="origin"/> to the next
    /// cell boundary ahead of it along the direction <paramref name="step"/>
    /// — the numerator of that axis's first parametric bound, still over
    /// the fixed denominator that axis's caller already knows (its total
    /// displacement magnitude). Always non-negative: <paramref name="step"/>
    /// is only ever called with the sign that points from
    /// <paramref name="origin"/>'s own cell boundary toward the next one
    /// ahead of it in the direction of travel.
    /// </summary>
    private static long DistanceToNextBoundary(long origin, int cell, int step)
    {
        var boundary = step > 0
            ? (long)(cell + 1) * NavGrid.CellSizeWu
            : (long)cell * NavGrid.CellSizeWu;

        return step > 0 ? boundary - origin : origin - boundary;
    }
}
