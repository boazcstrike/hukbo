namespace Sandata.Core.Navigation;

/// <summary>
/// Greedy line-of-sight smoothing over a nav grid corridor, per design
/// section 7's 2026-08-07 amendment, "a grid corridor needs line-of-sight
/// smoothing": anchor at the first corridor point; advance a probe to the
/// furthest corridor point still visible from the anchor; emit that point,
/// make it the new anchor, and repeat to the goal. Visibility is
/// <see cref="LineOfSight.IsVisible"/> — the exact-predicate, epsilon-free
/// test the shooting model already uses — so this type inherits the
/// determinism of a subsystem that is already pinned rather than introducing
/// a second geometry convention.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists alongside <see cref="Funnel"/>.</b> The amendment
/// measured that <see cref="Funnel.StringPull"/> alone does not deliver a
/// taut path over a grid A* corridor: a navmesh portal is as wide as the
/// polygons sharing it, but a grid corridor is a chain of single cells, so
/// every portal <see cref="Funnel"/> sees is exactly one cell edge wide, and
/// the funnel cannot straighten what the corridor never gave it room to
/// straighten. This type instead reasons about the corridor's actual cell
/// centres against the real wall geometry, which has no such width limit.
/// <see cref="Funnel"/> stays in the tree, off the v0.1 publish path, for the
/// day a navmesh or a widened corridor gives it room to work again.
/// </para>
/// <para>
/// <b>Minimum vertices, by construction.</b> Each anchor is chosen as the
/// furthest corridor point still visible from the previous anchor, so no
/// point further along was skippable — if it had been visible, the scan
/// would have chosen it instead. Consequently no emitted interior point can
/// be removed without breaking the visibility of one of its two adjacent
/// segments: the point before it could not see past it, and the point after
/// it exists precisely because it was the furthest thing that point *could*
/// see.
/// </para>
/// <para>
/// <b>The furthest-visible scan runs backward from the goal.</b> Visibility
/// along a corridor is not guaranteed to be monotonic in index — a corridor
/// can bend back toward the anchor's own sightline — so the scan for each
/// anchor starts at the corridor's last index and walks backward until it
/// finds a visible candidate, rather than walking forward and assuming the
/// first invisible index ends the visible run. This finds the true furthest
/// visible point regardless of the corridor's shape, at the cost of a
/// worst-case quadratic scan over a corridor whose cells are almost never
/// visible from each other; corridor lengths in this game are small per
/// design section 8's group counts, so that cost is not a concern.
/// </para>
/// </remarks>
public static class PathSmoothing
{
    /// <summary>
    /// Smooths <paramref name="corridor"/> — an ordered run of
    /// <paramref name="grid"/> cell indices, exactly the shape
    /// <see cref="NavSearch.TryFindPath"/> writes into its
    /// <c>pathCellIndices</c> output — into the minimum-vertex, line-of-sight
    /// clean polyline described in this type's remarks, writing each output
    /// point's coordinates into <paramref name="outputX"/> and
    /// <paramref name="outputY"/> in travel order.
    /// </summary>
    /// <param name="corridor">
    /// The cell corridor to smooth, start cell first, goal cell last. Must
    /// hold at least one cell.
    /// </param>
    /// <param name="outputX">
    /// Receives each output point's X coordinate, in world units, in travel
    /// order. Must be the same length as <paramref name="outputY"/> and hold
    /// at least <paramref name="corridor"/>'s length — see the remarks below
    /// on why that bound is never exceeded.
    /// </param>
    /// <param name="outputY">Receives each output point's Y coordinate. See <paramref name="outputX"/>.</param>
    /// <param name="grid">The grid <paramref name="corridor"/>'s indices are drawn from.</param>
    /// <param name="wallBuckets">The wall segment index <see cref="LineOfSight.IsVisible"/> queries against.</param>
    /// <returns>The number of points written into <paramref name="outputX"/> and <paramref name="outputY"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="wallBuckets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="corridor"/> is empty; <paramref name="outputX"/> and
    /// <paramref name="outputY"/> differ in length; or either output span is
    /// too short to hold <paramref name="corridor"/>'s length.
    /// </exception>
    /// <remarks>
    /// The output can never hold more points than <paramref name="corridor"/>
    /// holds cells: every emitted point advances the anchor to a strictly
    /// greater corridor index than the previous anchor (the fallback, when no
    /// further point is visible, is always the immediate next index), so the
    /// number of anchors visited — and therefore the number of points emitted
    /// — is bounded by the number of distinct corridor indices, which is
    /// <c>corridor.Length</c>.
    /// </remarks>
    public static int Smooth(
        ReadOnlySpan<int> corridor,
        Span<long> outputX,
        Span<long> outputY,
        NavGrid grid,
        WallBuckets wallBuckets)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        if (corridor.Length == 0)
        {
            throw new ArgumentException("The corridor must hold at least one cell.", nameof(corridor));
        }

        if (outputX.Length != outputY.Length)
        {
            throw new ArgumentException(
                $"outputX (length {outputX.Length}) and outputY (length {outputY.Length}) must be the same length.",
                nameof(outputX));
        }

        if (outputX.Length < corridor.Length)
        {
            throw new ArgumentException(
                $"The destination spans (length {outputX.Length}) must hold at least the corridor's length ({corridor.Length}).",
                nameof(outputX));
        }

        var (anchorX, anchorY) = CellCentre(grid, corridor[0]);
        var count = 0;
        outputX[count] = anchorX;
        outputY[count] = anchorY;
        count++;

        var anchorIndex = 0;
        var lastIndex = corridor.Length - 1;

        while (anchorIndex < lastIndex)
        {
            var chosenIndex = anchorIndex + 1;
            var chosenX = 0L;
            var chosenY = 0L;
            var found = false;

            for (var candidate = lastIndex; candidate > anchorIndex + 1; candidate--)
            {
                var (candidateX, candidateY) = CellCentre(grid, corridor[candidate]);
                if (LineOfSight.IsVisible(anchorX, anchorY, candidateX, candidateY, grid, wallBuckets))
                {
                    chosenIndex = candidate;
                    chosenX = candidateX;
                    chosenY = candidateY;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                (chosenX, chosenY) = CellCentre(grid, corridor[chosenIndex]);
            }

            anchorIndex = chosenIndex;
            anchorX = chosenX;
            anchorY = chosenY;

            outputX[count] = anchorX;
            outputY[count] = anchorY;
            count++;
        }

        return count;
    }

    /// <summary>The centre of <paramref name="cellIndex"/>'s cell, in world units — matching <see cref="Funnel"/>'s own private helper of the same name.</summary>
    private static (long X, long Y) CellCentre(NavGrid grid, int cellIndex)
    {
        var x = grid.CellX(cellIndex);
        var y = grid.CellY(cellIndex);
        const int halfCell = NavGrid.CellSizeWu / 2;
        return ((long)x * NavGrid.CellSizeWu + halfCell, (long)y * NavGrid.CellSizeWu + halfCell);
    }
}
