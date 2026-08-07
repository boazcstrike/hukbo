// Ported from GoRogue (https://github.com/Chris3606/GoRogue), whose field of
// view implementation carries this MIT licence:
//
//     MIT License
//
//     Copyright (c) 2023 Christopher Ridley
//
//     Permission is hereby granted, free of charge, to any person obtaining a
//     copy of this software and associated documentation files (the
//     "Software"), to deal in the Software without restriction, including
//     without limitation the rights to use, copy, modify, merge, publish,
//     distribute, sublicense, and/or sell copies of the Software, and to
//     permit persons to whom the Software is furnished to do so, subject to
//     the following conditions:
//
//     The above copyright notice and this permission notice shall be
//     included in all copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
//     OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//     MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//     NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//     LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//     OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//     WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// GoRogue's own field of view code is itself a C# rendering of Björn
// Bergström's recursive shadowcasting algorithm, published on RogueBasin
// (https://www.roguebasin.com/index.php/FOV_using_recursive_shadowcasting)
// and, in one floating-point form or another, the shared ancestor of the
// field of view code in most roguelike toolkits that followed it, libtcod's
// FOV_SHADOW option among them. This file keeps that algorithm's structure —
// the eight-octant transform table, and the per-row "start slope, end slope,
// blocked" bookkeeping that turns a single one-octant scan into a full field
// of view by symmetry — and replaces every floating-point slope comparison
// the original performs with an exact integer numerator/denominator pair
// compared by cross-multiplication, because design section 4 of
// docs/plans/2026-08-07-sandata-scaffold-design.md bans `float` and `double`
// from Sandata.Core outright: a slope computed once per battle differs from
// the same slope computed on a different machine or a different .NET version
// only if it is a `double`, and this port cannot risk that for a value that
// decides which cells a unit can see.

using Sandata.Core.Navigation;

namespace Sandata.Core.Sensing;

/// <summary>
/// Recursive shadowcasting field of view, integer only, operating directly
/// over a <see cref="NavGrid"/>'s <see cref="NavGrid.Passability"/>. This is
/// the algorithm design section 7's "The ported algorithms" paragraph names
/// for "the fog-of-war cell visibility layer that sits behind the per-unit
/// cone" — the per-unit vision cone itself is
/// <see cref="Geometry.VisionCone"/>, a separate and already-existing type
/// this one does not touch.
/// </summary>
/// <remarks>
/// <para>
/// <b>The algorithm, in outline.</b> The origin's own cell is always visible.
/// Everything else is found by scanning eight 45-degree octants, one at a
/// time, each through the same recursive one-octant scan remapped onto that
/// octant by a fixed sign/axis-swap transform (<see cref="OctantXx"/>,
/// <see cref="OctantXy"/>, <see cref="OctantYx"/>, <see cref="OctantYy"/>).
/// Within one octant, the scan walks outward row by row (<c>distance</c> from
/// 1 up to the radius); within a row it walks from the row's "steep" edge
/// toward the octant's central axis, tracking a shrinking angular sector —
/// <c>start</c> down to <c>end</c> — that widens back out again in a fresh
/// recursive call whenever a blocking cell's far edge narrows the sector
/// further, and narrows again from the other side once the blocking run
/// ends. A sector that has closed entirely (<c>start &lt; end</c>) has
/// nothing left to light and returns immediately. This structure, the
/// per-row slope bookkeeping, and the octant transform table are unchanged
/// from the source algorithm; see the eight-octant field of view diagrams on
/// the RogueBasin page cited above for the geometric picture this class
/// implements in integer form.
/// </para>
/// <para>
/// <b>Slopes as exact rational pairs, never as <c>float</c>.</b> The source
/// algorithm computes two slopes per candidate cell, <c>(dx - 0.5) / (dy +
/// 0.5)</c> and <c>(dx + 0.5) / (dy - 0.5)</c>, where <c>dx</c> ranges over
/// <c>[-distance, 0]</c> and <c>dy</c> is always <c>-distance</c> (so
/// strictly negative for every row this method ever scans, because the
/// origin's own row is never re-entered). Doubling both slopes to clear the
/// halves and then negating numerator and denominator together to make the
/// denominator positive — a value-preserving step, since multiplying a
/// fraction's numerator and denominator by the same nonzero constant never
/// changes the fraction — turns those two expressions into
/// <c>leftNumerator = 1 - 2 * dx</c> over <c>leftDenominator = 2 * distance -
/// 1</c>, and <c>rightNumerator = -1 - 2 * dx</c> over
/// <c>rightDenominator = 2 * distance + 1</c>, both with a denominator that
/// is always strictly positive for <c>distance &gt;= 1</c>. The sector
/// bounds <c>start</c> and <c>end</c> are seeded as the same kind of
/// fraction, <c>1/1</c> and <c>0/1</c>, so every slope this method ever holds
/// shares the same positive-denominator convention, and every comparison
/// between two of them is therefore a single cross-multiplication —
/// <c>an * bd</c> versus <c>bn * ad</c> — with no sign correction ever
/// needed, exactly the technique <see cref="GridRay"/> already uses for its
/// own parametric comparisons.
/// </para>
/// <para>
/// <b>No <c>Math.Sqrt</c>: distance is compared squared.</b> A candidate
/// cell's local offset <c>(dx, dy)</c> is lit only when
/// <c>dx * dx + dy * dy &lt;= radiusCells * radiusCells</c>, never by taking
/// a square root of either side. Because every octant transform in
/// <see cref="OctantXx"/> through <see cref="OctantYy"/> is a signed
/// permutation matrix — exactly one nonzero entry, always <c>+1</c> or
/// <c>-1</c>, in each row and column — squared distance is preserved when
/// <c>(dx, dy)</c> is mapped into map coordinates, so this one octant-local
/// squared comparison is exactly the Euclidean disc test in every octant,
/// not an approximation of it.
/// </para>
/// <para>
/// <b>Closed doors block sight.</b> A <see cref="NavCellFlags.Blocked"/> cell
/// and a <see cref="NavCellFlags.Door"/> cell both block this scan; only
/// <see cref="NavCellFlags.Open"/> lets it continue past. A closed door is
/// exactly as opaque as a wall for the same reason it is impassable to a
/// mover in <see cref="NavBake"/>'s bake: nothing about a door being shut
/// lets a spectator or an operator see through it. Once a door opens,
/// <see cref="NavBake"/> rebakes its cells to <see cref="NavCellFlags.Open"/>
/// and this method — which always reads <see cref="NavGrid.Passability"/>
/// fresh, never a cached copy of it — stops treating that cell as blocking on
/// the very next call, with no separate wiring required.
/// </para>
/// <para>
/// <b>Out of bounds is transparent, not opaque.</b> A candidate cell outside
/// the grid is neither lit nor treated as a blocker: the scan simply skips it
/// and continues the row, exactly as it would skip a cell whose slope has not
/// yet entered the current sector. Restricting a battle map to a finite grid
/// must not manufacture a phantom wall at its edge that a wall never
/// occupying that cell should not be able to cast a shadow from.
/// </para>
/// </remarks>
public static class Shadowcast
{
    // The eight-octant transform table, unchanged from the source algorithm:
    // for octant `o`, `(xx, xy, yx, yy) = (OctantXx[o], OctantXy[o],
    // OctantYx[o], OctantYy[o])` remaps this type's single canonical
    // one-octant scan — always `dy = -distance`, `dx` from `-distance` to
    // `0` — onto the true map offset `(dx * xx + dy * xy, dx * yx + dy *
    // yy)`. Every entry is `1`, `0`, or `-1`, and each of the four arrays has
    // exactly one nonzero entry per octant column paired with the other
    // array in its row having the other nonzero entry — the signed
    // permutation property the class remarks rely on to keep squared
    // distance invariant across octants.
    private static readonly int[] OctantXx = [1, 0, 0, -1, -1, 0, 0, 1];
    private static readonly int[] OctantXy = [0, 1, -1, 0, 0, -1, 1, 0];
    private static readonly int[] OctantYx = [0, 1, 1, 0, 0, -1, -1, 0];
    private static readonly int[] OctantYy = [1, 0, 0, 1, -1, 0, 0, -1];

    /// <summary>
    /// Computes the set of cells visible from <paramref name="originCellIndex"/>
    /// out to <paramref name="radiusCells"/> cells, writing the result into
    /// <paramref name="field"/>. <paramref name="field"/> is not cleared
    /// first — a caller that wants a fresh result rather than the union of
    /// this call with whatever <paramref name="field"/> already held calls
    /// <see cref="VisibilityField.Clear"/> before calling this method.
    /// </summary>
    /// <param name="grid">
    /// Supplies the grid's bounds, cell index arithmetic, and current
    /// <see cref="NavGrid.Passability"/>.
    /// </param>
    /// <param name="originCellIndex">
    /// The cell the field of view is computed from. Always marked visible,
    /// regardless of its own passability — a unit standing on a door or, in
    /// principle, inside a wall still sees its own cell.
    /// </param>
    /// <param name="radiusCells">
    /// The maximum distance, in whole cells, a cell may be from the origin
    /// and still be lit. Zero means only the origin's own cell is visible.
    /// Must not be negative.
    /// </param>
    /// <param name="field">
    /// Receives the result. Must cover exactly <paramref name="grid"/>'s
    /// <see cref="NavGrid.CellCount"/> cells.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="grid"/> or <paramref name="field"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="originCellIndex"/> is outside <paramref name="grid"/>'s
    /// bounds, or <paramref name="radiusCells"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="field"/>'s <see cref="VisibilityField.CellCount"/> does
    /// not match <paramref name="grid"/>'s <see cref="NavGrid.CellCount"/>.
    /// </exception>
    public static void Compute(NavGrid grid, int originCellIndex, int radiusCells, VisibilityField field)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(field);

        if (originCellIndex < 0 || originCellIndex >= grid.CellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originCellIndex),
                originCellIndex,
                $"The origin cell index must be within [0, {grid.CellCount}).");
        }

        if (radiusCells < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusCells), radiusCells, "The radius cannot be negative.");
        }

        if (field.CellCount != grid.CellCount)
        {
            throw new ArgumentException(
                $"The visibility field has {field.CellCount} cells but the grid has {grid.CellCount}; they must match.",
                nameof(field));
        }

        var originX = grid.CellX(originCellIndex);
        var originY = grid.CellY(originCellIndex);

        field.MarkVisible(originCellIndex);

        for (var octant = 0; octant < 8; octant++)
        {
            CastLight(
                grid,
                field,
                originX,
                originY,
                row: 1,
                startNumerator: 1,
                startDenominator: 1,
                endNumerator: 0,
                endDenominator: 1,
                radiusCells,
                OctantXx[octant],
                OctantXy[octant],
                OctantYx[octant],
                OctantYy[octant]);
        }
    }

    /// <summary>
    /// The recursive one-octant scan. <paramref name="startNumerator"/> over
    /// <paramref name="startDenominator"/> and <paramref name="endNumerator"/>
    /// over <paramref name="endDenominator"/> are the sector's near and far
    /// slope bounds, in the positive-denominator rational form this class's
    /// remarks describe; <paramref name="row"/> is the first
    /// <c>distance</c> this call scans, always one past the row whose
    /// blocking cell spawned it (or <c>1</c> for the top-level call from
    /// <see cref="Compute"/>).
    /// </summary>
    private static void CastLight(
        NavGrid grid,
        VisibilityField field,
        int originX,
        int originY,
        int row,
        long startNumerator,
        long startDenominator,
        long endNumerator,
        long endDenominator,
        int radiusCells,
        int xx,
        int xy,
        int yx,
        int yy)
    {
        if (IsLess(startNumerator, startDenominator, endNumerator, endDenominator))
        {
            // The sector has narrowed to nothing: its near edge has crossed
            // its far edge, so there is no light left for this call to cast
            // at any distance. This is the source algorithm's own "start <
            // end: return" early exit, carried over unchanged.
            return;
        }

        var radiusSquared = (long)radiusCells * radiusCells;
        var blocked = false;
        var nextStartNumerator = 0L;
        var nextStartDenominator = 1L;

        for (var distance = row; distance <= radiusCells; distance++)
        {
            var dy = -distance;

            for (var dx = -distance; dx <= 0; dx++)
            {
                var mapX = originX + (dx * xx) + (dy * xy);
                var mapY = originY + (dx * yx) + (dy * yy);

                // Both denominators below are strictly positive for every
                // distance >= 1 reached by this loop (2 * distance - 1 and
                // 2 * distance + 1 are both at least 1), so every comparison
                // against these two fractions is a plain cross-multiplication
                // — see this class's remarks for the derivation from the
                // source algorithm's (dx +/- 0.5) / (dy +/- 0.5) slopes.
                var leftNumerator = 1 - (2L * dx);
                var leftDenominator = (2L * distance) - 1;
                var rightNumerator = -1 - (2L * dx);
                var rightDenominator = (2L * distance) + 1;

                if (!grid.IsInBounds(mapX, mapY))
                {
                    // Out of bounds is transparent, not opaque: see this
                    // class's remarks. The scan simply has nothing to look
                    // at here and moves on to the next cell in the row.
                    continue;
                }

                if (IsLess(startNumerator, startDenominator, rightNumerator, rightDenominator))
                {
                    // This cell's right edge is still beyond the sector's
                    // near edge: the sweep has not yet reached it.
                    continue;
                }

                if (IsGreater(endNumerator, endDenominator, leftNumerator, leftDenominator))
                {
                    // This cell's left edge already lies past the sector's
                    // far edge, so every remaining cell in this row (moving
                    // toward dx == 0) is out of the sector too.
                    break;
                }

                var localDistanceSquared = ((long)dx * dx) + ((long)dy * dy);
                if (localDistanceSquared <= radiusSquared)
                {
                    field.MarkVisible(grid.CellIndex(mapX, mapY));
                }

                var cellBlocksSight = BlocksSight(grid, mapX, mapY);

                if (blocked)
                {
                    if (cellBlocksSight)
                    {
                        // Still inside a run of blocking cells: keep pushing
                        // the sector's near edge in for when the run ends.
                        nextStartNumerator = rightNumerator;
                        nextStartDenominator = rightDenominator;
                        continue;
                    }

                    // The blocking run just ended: resume scanning this same
                    // row with the sector's near edge narrowed to where the
                    // run stopped.
                    blocked = false;
                    startNumerator = nextStartNumerator;
                    startDenominator = nextStartDenominator;
                }
                else if (cellBlocksSight && distance < radiusCells)
                {
                    // A blocker just started, and there is at least one more
                    // row this octant could still reach: everything strictly
                    // beyond this blocker, out to its far edge, gets its own
                    // recursive scan starting one row further out, before
                    // this row continues past the blocker with its near edge
                    // narrowed.
                    blocked = true;
                    CastLight(
                        grid,
                        field,
                        originX,
                        originY,
                        distance + 1,
                        startNumerator,
                        startDenominator,
                        leftNumerator,
                        leftDenominator,
                        radiusCells,
                        xx,
                        xy,
                        yx,
                        yy);

                    nextStartNumerator = rightNumerator;
                    nextStartDenominator = rightDenominator;
                }
            }

            if (blocked)
            {
                // The row ended while still inside a blocking run: nothing
                // farther out in this octant's sector can be reached, either,
                // because the run never opened back up before the row ran
                // out of cells to scan.
                break;
            }
        }
    }

    /// <summary>
    /// Whether the cell at map coordinate <paramref name="x"/>,
    /// <paramref name="y"/> blocks this scan: a
    /// <see cref="NavCellFlags.Blocked"/> or <see cref="NavCellFlags.Door"/>
    /// cell blocks; a <see cref="NavCellFlags.Open"/> cell does not. See this
    /// class's remarks, "Closed doors block sight."
    /// </summary>
    private static bool BlocksSight(NavGrid grid, int x, int y)
    {
        var flags = grid.Passability[grid.CellIndex(x, y)];
        return flags is NavCellFlags.Blocked or NavCellFlags.Door;
    }

    /// <summary>
    /// Whether the fraction <paramref name="leftNumerator"/> over
    /// <paramref name="leftDenominator"/> is strictly less than
    /// <paramref name="rightNumerator"/> over
    /// <paramref name="rightDenominator"/>, by cross-multiplication. Both
    /// denominators must already be strictly positive; every caller in this
    /// file only ever holds slopes in that normalised form.
    /// </summary>
    private static bool IsLess(long leftNumerator, long leftDenominator, long rightNumerator, long rightDenominator) =>
        leftNumerator * rightDenominator < rightNumerator * leftDenominator;

    /// <summary>
    /// The mirror of <see cref="IsLess"/>: whether the left fraction is
    /// strictly greater than the right one.
    /// </summary>
    private static bool IsGreater(long leftNumerator, long leftDenominator, long rightNumerator, long rightDenominator) =>
        leftNumerator * rightDenominator > rightNumerator * leftDenominator;
}
