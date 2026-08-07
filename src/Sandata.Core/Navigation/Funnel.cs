// Ported from DotRecast (https://github.com/ikpil/DotRecast), itself a C#
// port of Recast Navigation (https://github.com/recastnavigation/recastnavigation).
// Both are distributed under the zlib licence, which requires only that the
// origin of the software not be misrepresented and that this notice not be
// removed from the source distribution — no further permission, royalty, or
// attribution-beyond-notice is required to use, modify, or redistribute it,
// in source or binary form. See ThirdPartyNotices.md in this directory for
// the full notice this project carries for that dependency.
//
// The algorithm ported here is Recast/Detour's "simple stupid funnel"
// string-pull (the loop inside DtNavMeshQuery.FindStraightPath in
// DtNavMeshQuery.cs), rewritten from float triarea2 tests over navmesh
// polygon portals to integer Orient tests over NavGrid cell-edge portals.
// Two behaviours are carried over deliberately, not invented here:
// the apex/left/right funnel state with its insert-and-restart-scan
// crossover handling, and AppendVertex's exact-equality dedup against only
// the most recently written point (real DotRecast: if the new point equals
// straightPath[straightPathCount - 1], no new entry is added).

using Sandata.Core.Geometry;

namespace Sandata.Core.Navigation;

/// <summary>
/// Recast's "simple stupid funnel" string-pull algorithm, ported to integer
/// arithmetic from DotRecast (zlib licence, attribution only — see the
/// header above and ThirdPartyNotices.md). Design section 7, "Shape":
/// "Uniform integer grid A* decides topology; a funnel string-pull snaps the
/// resulting corridor to the real vector wall geometry" — this type is that
/// second half. It consumes the ordered cell corridor <see cref="NavSearch.TryFindPath"/>
/// found and taut-strings a polyline through it, so a unit crossing a
/// shallow-angle corridor walks one straight segment instead of the grid's
/// own staircase of cell centres.
/// </summary>
/// <remarks>
/// <para>
/// <b>Portals.</b> Every pair of consecutive corridor cells shares either an
/// edge (an orthogonal step) or a single corner (a diagonal step). This type
/// turns that shared boundary into a "portal" — a left point and a right
/// point the string must pass between — by walking the departing cell's own
/// four corners in one fixed order, top-left, top-right, bottom-right,
/// bottom-left, and reading off the two corners (or, for a diagonal step,
/// the one shared corner) that face the direction of travel. Because this
/// per-cell winding is the same for every cell regardless of which direction
/// the corridor is coming from or turning toward, "left" and "right" stay
/// consistent across a corridor that changes direction, exactly the way a
/// real navmesh's precomputed, consistent polygon winding does — a portal
/// derived instead from the two cell centres' local travel direction was
/// tried and hand-traced first, and produces an inconsistent left/right
/// labelling of the same physical corner across a turn, which is why it was
/// discarded in favour of this fixed, per-cell rule.
/// </para>
/// <para>
/// <b>The funnel loop.</b> Starting with the apex, the left bound, and the
/// right bound all at the corridor's start point, each subsequent portal
/// either tightens whichever bound it narrows (the string can still reach
/// it without crossing the other bound) or, when a portal would force the
/// bounds to cross, the funnel commits to the bound that was about to be
/// crossed — appending it to the output, making it the new apex, and
/// restarting the scan from there. This is the standard Recast/Detour
/// funnel; only the geometry underneath it (integer grid-cell portals
/// instead of floating-point navmesh polygon portals) is specific to this
/// port.
/// </para>
/// <para>
/// <b>Only <see cref="ExactPredicates.Orient"/>.</b> Every left/right/inside
/// test in the loop is one call to <see cref="ExactPredicates.Orient"/>; this
/// type defines no cross product or other geometric primitive of its own,
/// per task 56's consolidation onto that single predicate.
/// </para>
/// </remarks>
public static class Funnel
{
    /// <summary>
    /// Directions between two orthogonally or diagonally adjacent corridor
    /// cells, as an (X, Y) delta. Only used to select which pair of the
    /// departing cell's four corners forms the portal toward the next cell.
    /// </summary>
    private const int East = 1;

    private const int West = -1;

    private const int North = -1;

    private const int South = 1;

    private const int Stay = 0;

    /// <summary>
    /// Runs the funnel over <paramref name="corridor"/> — an ordered run of
    /// <paramref name="grid"/> cell indices in which every consecutive pair
    /// is orthogonally or diagonally adjacent, exactly the shape
    /// <see cref="NavSearch.TryFindPath"/> writes into its
    /// <c>pathCellIndices</c> output — and writes the taut-strung polyline's
    /// points, in travel order, into <paramref name="outputX"/> and
    /// <paramref name="outputY"/>.
    /// </summary>
    /// <param name="corridor">
    /// The cell corridor to string-pull, start cell first, goal cell last.
    /// Must hold at least one cell, and every consecutive pair must be one
    /// of the eight <see cref="NavNeighbors"/> steps apart.
    /// </param>
    /// <param name="outputX">
    /// Receives each output point's X coordinate, in world units, in travel
    /// order. Must be the same length as <paramref name="outputY"/>. If the
    /// string-pulled path would hold more points than this span holds, the
    /// walk stops early and returns the number of points actually written;
    /// it never writes past the end of either output span.
    /// </param>
    /// <param name="outputY">Receives each output point's Y coordinate. See <paramref name="outputX"/>.</param>
    /// <param name="grid">The grid <paramref name="corridor"/>'s indices are drawn from.</param>
    /// <returns>The number of points written into <paramref name="outputX"/> and <paramref name="outputY"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="corridor"/> is empty; <paramref name="outputX"/> and
    /// <paramref name="outputY"/> differ in length; either output span is
    /// empty; or two consecutive corridor cells are not adjacent.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A corridor cell index lies outside <paramref name="grid"/>.</exception>
    public static int StringPull(ReadOnlySpan<int> corridor, Span<long> outputX, Span<long> outputY, NavGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

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

        if (outputX.Length == 0)
        {
            throw new ArgumentException("The destination spans must hold at least one point.", nameof(outputX));
        }

        for (var index = 0; index < corridor.Length; index++)
        {
            ValidateCellIndex(grid, corridor[index], nameof(corridor));
        }

        var capacity = outputX.Length;
        var (startX, startY) = CellCentre(grid, corridor[0]);

        var apexX = startX;
        var apexY = startY;
        var leftX = startX;
        var leftY = startY;
        var rightX = startX;
        var rightY = startY;
        var apexIndex = 0;
        var leftIndex = 0;
        var rightIndex = 0;

        var count = 0;
        Append(outputX, outputY, ref count, apexX, apexY);

        var lastPortalIndex = corridor.Length;
        for (var i = 1; i <= lastPortalIndex && count < capacity; i++)
        {
            GetPortal(grid, corridor, i, out var liX, out var liY, out var riX, out var riY);

            if (ExactPredicates.Orient(apexX, apexY, rightX, rightY, riX, riY) <= 0)
            {
                if ((apexX == rightX && apexY == rightY) ||
                    ExactPredicates.Orient(apexX, apexY, leftX, leftY, riX, riY) > 0)
                {
                    rightX = riX;
                    rightY = riY;
                    rightIndex = i;
                }
                else
                {
                    Append(outputX, outputY, ref count, leftX, leftY);

                    apexX = leftX;
                    apexY = leftY;
                    apexIndex = leftIndex;

                    leftX = rightX = apexX;
                    leftY = rightY = apexY;
                    leftIndex = rightIndex = apexIndex;

                    i = apexIndex;
                    continue;
                }
            }

            if (ExactPredicates.Orient(apexX, apexY, leftX, leftY, liX, liY) >= 0)
            {
                if ((apexX == leftX && apexY == leftY) ||
                    ExactPredicates.Orient(apexX, apexY, rightX, rightY, liX, liY) < 0)
                {
                    leftX = liX;
                    leftY = liY;
                    leftIndex = i;
                }
                else
                {
                    Append(outputX, outputY, ref count, rightX, rightY);

                    apexX = rightX;
                    apexY = rightY;
                    apexIndex = rightIndex;

                    leftX = rightX = apexX;
                    leftY = rightY = apexY;
                    leftIndex = rightIndex = apexIndex;

                    i = apexIndex;
                    continue;
                }
            }
        }

        var (endX, endY) = CellCentre(grid, corridor[^1]);
        Append(outputX, outputY, ref count, endX, endY);

        return count;
    }

    /// <summary>
    /// Writes the left and right portal points for the transition at
    /// <paramref name="portalIndex"/> — either the degenerate start portal
    /// (index 0, both points the corridor's first cell centre, handled by
    /// the caller rather than here), the degenerate end portal (index equal
    /// to <c>corridor.Length</c>, both points the corridor's last cell
    /// centre), or the shared-boundary portal between
    /// <c>corridor[portalIndex - 1]</c> (the departing cell) and
    /// <c>corridor[portalIndex]</c> (the arriving cell).
    /// </summary>
    private static void GetPortal(
        NavGrid grid,
        ReadOnlySpan<int> corridor,
        int portalIndex,
        out long leftX,
        out long leftY,
        out long rightX,
        out long rightY)
    {
        if (portalIndex == corridor.Length)
        {
            var (endX, endY) = CellCentre(grid, corridor[^1]);
            leftX = rightX = endX;
            leftY = rightY = endY;
            return;
        }

        var fromCellIndex = corridor[portalIndex - 1];
        var toCellIndex = corridor[portalIndex];

        var fromX = grid.CellX(fromCellIndex);
        var fromY = grid.CellY(fromCellIndex);
        var toX = grid.CellX(toCellIndex);
        var toY = grid.CellY(toCellIndex);

        var deltaX = Math.Sign(toX - fromX);
        var deltaY = Math.Sign(toY - fromY);

        var topLeftX = (long)fromX * NavGrid.CellSizeWu;
        var topLeftY = (long)fromY * NavGrid.CellSizeWu;
        var bottomRightX = (long)(fromX + 1) * NavGrid.CellSizeWu;
        var bottomRightY = (long)(fromY + 1) * NavGrid.CellSizeWu;

        // The departing cell's own four corners, walked in one fixed order
        // (top-left, top-right, bottom-right, bottom-left) that never
        // depends on which way the corridor is travelling. Every portal
        // below reads two of these four points (or, for a diagonal step,
        // the single point the two cells share).
        switch (deltaX, deltaY)
        {
            case (East, Stay):
                // Shared edge is the departing cell's east side: top-right
                // to bottom-right.
                leftX = bottomRightX;
                leftY = topLeftY;
                rightX = bottomRightX;
                rightY = bottomRightY;
                break;

            case (Stay, South):
                // Shared edge is the departing cell's south side:
                // bottom-right to bottom-left.
                leftX = bottomRightX;
                leftY = bottomRightY;
                rightX = topLeftX;
                rightY = bottomRightY;
                break;

            case (West, Stay):
                // Shared edge is the departing cell's west side:
                // bottom-left to top-left.
                leftX = topLeftX;
                leftY = bottomRightY;
                rightX = topLeftX;
                rightY = topLeftY;
                break;

            case (Stay, North):
                // Shared edge is the departing cell's north side: top-left
                // to top-right.
                leftX = topLeftX;
                leftY = topLeftY;
                rightX = bottomRightX;
                rightY = topLeftY;
                break;

            case (East, South):
                // Diagonal step: the two cells share only the departing
                // cell's bottom-right corner.
                leftX = rightX = bottomRightX;
                leftY = rightY = bottomRightY;
                break;

            case (West, South):
                // Diagonal step: shared corner is the departing cell's
                // bottom-left.
                leftX = rightX = topLeftX;
                leftY = rightY = bottomRightY;
                break;

            case (West, North):
                // Diagonal step: shared corner is the departing cell's
                // top-left.
                leftX = rightX = topLeftX;
                leftY = rightY = topLeftY;
                break;

            case (East, North):
                // Diagonal step: shared corner is the departing cell's
                // top-right.
                leftX = rightX = bottomRightX;
                leftY = rightY = topLeftY;
                break;

            default:
                throw new ArgumentException(
                    $"Corridor cells at index {portalIndex - 1} and {portalIndex} are not adjacent " +
                    $"(cell ({fromX}, {fromY}) to cell ({toX}, {toY})).",
                    nameof(corridor));
        }
    }

    /// <summary>
    /// Appends <paramref name="x"/>, <paramref name="y"/> to the output
    /// spans, faithfully reproducing DotRecast's <c>AppendVertex</c>
    /// dedup rule: if this point is exactly equal to the most recently
    /// appended one, nothing is written and <paramref name="count"/> does
    /// not change. This is what keeps the final unconditional end-point
    /// append from producing a trailing duplicate whenever the funnel's own
    /// last commit already landed exactly on the corridor's end point.
    /// </summary>
    private static void Append(Span<long> outputX, Span<long> outputY, ref int count, long x, long y)
    {
        if (count > 0 && outputX[count - 1] == x && outputY[count - 1] == y)
        {
            return;
        }

        if (count >= outputX.Length)
        {
            return;
        }

        outputX[count] = x;
        outputY[count] = y;
        count++;
    }

    /// <summary>The centre of <paramref name="cellIndex"/>'s cell, in world units.</summary>
    private static (long X, long Y) CellCentre(NavGrid grid, int cellIndex)
    {
        var x = grid.CellX(cellIndex);
        var y = grid.CellY(cellIndex);
        const int halfCell = NavGrid.CellSizeWu / 2;
        return ((long)x * NavGrid.CellSizeWu + halfCell, (long)y * NavGrid.CellSizeWu + halfCell);
    }

    private static void ValidateCellIndex(NavGrid grid, int cellIndex, string paramName)
    {
        if (cellIndex < 0 || cellIndex >= grid.CellCount)
        {
            throw new ArgumentOutOfRangeException(
                paramName, cellIndex, $"Cell index must be within [0, {grid.CellCount}).");
        }
    }
}
