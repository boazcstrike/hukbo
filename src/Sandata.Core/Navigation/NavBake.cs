using Sandata.Core.Maps;

namespace Sandata.Core.Navigation;

/// <summary>
/// Bakes a <see cref="NavGrid"/>'s <see cref="NavGrid.Passability"/> from a
/// map's <c>WALL</c> and closed <c>DOOR</c> records and one mover body
/// radius, per plan task 18 and design section 7. Three things happen, in
/// this order, every time <see cref="Bake"/> runs:
///
/// <list type="number">
/// <item>Every <see cref="WallRecord"/> and every closed
/// <see cref="DoorRecord"/> is rasterized into the grid with an integer
/// supercover walk (<see cref="SupercoverCells"/>) — no gap at a diagonal
/// corner a body could otherwise slip through. Walls always win: a cell any
/// wall segment's supercover touches is <see cref="NavCellFlags.Blocked"/>
/// and stays that way regardless of a door record also touching it.</item>
/// <item>Every cell within the baked body radius, in cells, of a
/// <see cref="NavCellFlags.Blocked"/> cell — Chebyshev (king-move) distance,
/// not Euclidean, so this stays a <c>double</c>-free integer computation —
/// is itself forced <see cref="NavCellFlags.Blocked"/>. This is what turns
/// "a point fits here" into "a body of the baked radius fits here", and it
/// is also what narrows a doorway: design section 7's worked example, a 0.9
/// m doorway three cells wide before inflation, is one cell wide after it
/// because the flanking walls' inflation eats the two side cells and only
/// the center survives.</item>
/// <item>A closed door's own cells keep the <see cref="NavCellFlags.Door"/>
/// tag from step 1 unless step 1 already gave that cell to a wall — inflation
/// from a nearby wall does <b>not</b> demote a surviving door cell to
/// <see cref="NavCellFlags.Blocked"/>, because inflation only ever originates
/// from a wall-blocked source cell (see <see cref="InflateByBodyRadius"/>);
/// a closed door is not itself an inflation source. A door cell that
/// survives is passable to the planner at high cost and impassable to the
/// mover until the door opens, at which point a later stage rebakes the
/// affected cells to <see cref="NavCellFlags.Open"/>.</item>
/// </list>
///
/// <para>
/// <b>Every array this method writes is derived, not authoritative, state.</b>
/// Nothing here is stored in a save, folded into the state hash, or captured
/// in a snapshot — it is cheap to rebuild from the map's own records plus the
/// body radius, on load and again whenever a door's state changes
/// (<c>CLAUDE.md</c> section 9's "Save derived caches, render data, or
/// metrics into a snapshot" do-not; design section 4's derived-versus-hashed
/// split). <see cref="Bake"/> resets <see cref="NavGrid.Passability"/> to
/// <see cref="NavCellFlags.Open"/> before rasterizing anything, so calling it
/// again — after a door opens, or with a different body radius — starts from
/// a clean slate rather than compounding a stale bake.
/// </para>
/// </summary>
public static class NavBake
{
    /// <summary>Design section 12: a <c>DOOR</c> record's <c>State</c> field is 0 when closed, 1 when open.</summary>
    private const int DoorStateClosed = 0;

    /// <summary>
    /// Rebuilds <paramref name="grid"/>'s <see cref="NavGrid.Passability"/>
    /// in place from <paramref name="walls"/>, the closed doors among
    /// <paramref name="doors"/>, and a mover of <paramref name="bodyRadiusWu"/>
    /// world units' radius.
    /// </summary>
    /// <param name="grid">
    /// The grid whose <see cref="NavGrid.Passability"/> array is overwritten.
    /// Its own array instance is reused; no new array is allocated for it.
    /// </param>
    /// <param name="walls">Every <c>WALL</c> record on the map, in any order — rasterization order never affects the result.</param>
    /// <param name="doors">Every <c>DOOR</c> record on the map, in any order. An open door (<c>State</c> 1) is skipped entirely.</param>
    /// <param name="bodyRadiusWu">
    /// The mover's body radius, in whole world units. <see cref="Sandata.Core"/>
    /// cannot reference <c>Hukbo.Core</c>'s shared body-radius constant, so
    /// every caller supplies its own value explicitly, matching
    /// <see cref="Collision.SandataCollisionGrid"/>'s established convention
    /// in this project. Must be strictly positive.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/>, <paramref name="walls"/>, or <paramref name="doors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bodyRadiusWu"/> is zero or negative.</exception>
    public static void Bake(
        NavGrid grid,
        IReadOnlyList<WallRecord> walls,
        IReadOnlyList<DoorRecord> doors,
        int bodyRadiusWu)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(doors);

        if (bodyRadiusWu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusWu),
                bodyRadiusWu,
                "Body radius must be strictly positive.");
        }

        var passability = grid.Passability;
        Array.Fill(passability, NavCellFlags.Open);

        foreach (var wall in walls)
        {
            RasterizeSegment(
                passability, grid, wall.X1, wall.Y1, wall.X2, wall.Y2,
                value: NavCellFlags.Blocked, skipIfAlreadyBlocked: false);
        }

        foreach (var door in doors)
        {
            if (door.State != DoorStateClosed)
            {
                continue;
            }

            RasterizeSegment(
                passability, grid, door.X1, door.Y1, door.X2, door.Y2,
                value: NavCellFlags.Door, skipIfAlreadyBlocked: true);
        }

        InflateByBodyRadius(grid, passability, bodyRadiusWu);
    }

    /// <summary>
    /// Walks <see cref="SupercoverCells"/> for one segment and, for every
    /// touched cell that lies inside <paramref name="grid"/>, writes
    /// <paramref name="value"/> — unless <paramref name="skipIfAlreadyBlocked"/>
    /// is set and the cell is already <see cref="NavCellFlags.Blocked"/>, the
    /// rule that lets a wall's rasterization always win over a door record
    /// touching the same cell.
    ///
    /// <para>
    /// A touched cell coordinate that falls outside <paramref name="grid"/>
    /// is silently skipped rather than thrown: design section 12 validates a
    /// segment's endpoints against the closed interval
    /// <c>[0, WidthWu] x [0, HeightWu]</c>, so an endpoint may legitimately
    /// sit exactly on the map's far edge. <see cref="NavGrid.WorldToCellCoordinate"/>
    /// places that point one cell past the last valid column or row — the
    /// zero-width outer boundary of the map, not a real cell to rasterize.
    /// </para>
    /// </summary>
    private static void RasterizeSegment(
        NavCellFlags[] passability,
        NavGrid grid,
        int x1,
        int y1,
        int x2,
        int y2,
        NavCellFlags value,
        bool skipIfAlreadyBlocked)
    {
        foreach (var (x, y) in SupercoverCells(x1, y1, x2, y2))
        {
            if (!grid.TryGetCellIndex(x, y, out var index))
            {
                continue;
            }

            if (skipIfAlreadyBlocked && passability[index] == NavCellFlags.Blocked)
            {
                continue;
            }

            passability[index] = value;
        }
    }

    /// <summary>
    /// Enumerates, in supercover order, every cell coordinate the straight
    /// world-unit segment from (<paramref name="x1"/>, <paramref name="y1"/>)
    /// to (<paramref name="x2"/>, <paramref name="y2"/>) passes through — an
    /// integer, division-free walk that never misses a cell the line only
    /// grazes at a corner, so a wall's rasterization never leaves a
    /// diagonal-sized gap a body could slip through.
    ///
    /// <para>
    /// The walk steps from the start cell to the end cell one axis at a time,
    /// choosing whichever of the next vertical or horizontal grid line the
    /// segment reaches first. That choice is made by comparing the two
    /// crossing fractions <c>t_x</c> and <c>t_y</c> without ever computing
    /// either one: for a segment with direction (<c>dx</c>, <c>dy</c>),
    /// <c>t_x &lt; t_y</c> is equivalent to
    /// <c>numeratorX * |dy| &lt; numeratorY * |dx|</c>, where
    /// <c>numeratorX</c>/<c>numeratorY</c> are each already non-negative by
    /// construction (the step direction cancels the sign of the
    /// corresponding delta), so the cross-multiplication needs no sign
    /// correction and no division.
    /// </para>
    ///
    /// <para>
    /// When the two fractions tie exactly — the segment passes through the
    /// precise corner shared by four cells — this walk steps X first, then Y,
    /// visiting both of the two cells that share that corner along the
    /// segment's direction of travel, rather than jumping diagonally past
    /// them. This mirrors the "step X first" diagonal-corner tie rule pinned
    /// elsewhere in this project's navigation code (design section 7); it is
    /// this rasterizer's own implementation of that rule, not a call into
    /// task 20's <c>GridRay</c>, which this task does not depend on and may
    /// not exist yet in the tree this bakes against.
    /// </para>
    /// </summary>
    /// <summary>
    /// Internal rather than private so <see cref="RoomLayout.Bake"/> can reuse
    /// this exact pinned integer supercover walk for its own room-boundary
    /// rasterization, which — unlike <see cref="Bake"/> above — must treat
    /// every <c>DOOR</c> record as boundary geometry regardless of its
    /// <c>State</c>. Widening this one method's accessibility is the only
    /// change made to it; its algorithm and behavior are untouched.
    /// </summary>
    internal static IEnumerable<(int X, int Y)> SupercoverCells(int x1, int y1, int x2, int y2)
    {
        var cx = NavGrid.WorldToCellCoordinate(x1);
        var cy = NavGrid.WorldToCellCoordinate(y1);
        var ex = NavGrid.WorldToCellCoordinate(x2);
        var ey = NavGrid.WorldToCellCoordinate(y2);

        yield return (cx, cy);

        if (cx == ex && cy == ey)
        {
            yield break;
        }

        long dx = x2 - x1;
        long dy = y2 - y1;
        var stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        var stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;
        var absDx = dx < 0 ? -dx : dx;
        var absDy = dy < 0 ? -dy : dy;

        while (cx != ex || cy != ey)
        {
            var canStepX = stepX != 0 && cx != ex;
            var canStepY = stepY != 0 && cy != ey;

            if (canStepX && canStepY)
            {
                checked
                {
                    long nextVerticalLine = stepX > 0
                        ? (long)(cx + 1) * NavGrid.CellSizeWu
                        : (long)cx * NavGrid.CellSizeWu;
                    long nextHorizontalLine = stepY > 0
                        ? (long)(cy + 1) * NavGrid.CellSizeWu
                        : (long)cy * NavGrid.CellSizeWu;

                    var numeratorX = stepX * (nextVerticalLine - x1);
                    var numeratorY = stepY * (nextHorizontalLine - y1);
                    var compare = (numeratorX * absDy).CompareTo(numeratorY * absDx);

                    if (compare <= 0)
                    {
                        cx += stepX;
                        yield return (cx, cy);

                        if (compare == 0)
                        {
                            cy += stepY;
                            yield return (cx, cy);
                        }
                    }
                    else
                    {
                        cy += stepY;
                        yield return (cx, cy);
                    }
                }
            }
            else if (canStepX)
            {
                cx += stepX;
                yield return (cx, cy);
            }
            else if (canStepY)
            {
                cy += stepY;
                yield return (cx, cy);
            }
            else
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Forces every cell within <paramref name="bodyRadiusWu"/>, converted to
    /// whole cells and measured by Chebyshev distance, of an originally
    /// wall-blocked cell to <see cref="NavCellFlags.Blocked"/> — the "a body
    /// fits here" pass. Chebyshev rather than Euclidean distance because a
    /// square inflation footprint is exact integer arithmetic, with no
    /// <c>double</c> and no <see cref="Math.Sqrt"/> anywhere in
    /// <see cref="Sandata.Core"/> (design section 4).
    ///
    /// <para>
    /// Sources are snapshotted before any cell is mutated, so inflating one
    /// wall's footprint never cascades into treating a cell that became
    /// blocked only because of inflation as a further inflation source — the
    /// baked radius stays exactly the body radius from every real wall, never
    /// larger. A door cell is never a source: only a cell that step 1 marked
    /// <see cref="NavCellFlags.Blocked"/> (a wall) inflates; a closed door
    /// cell does not, so a door cell far enough from any wall survives
    /// inflation with its <see cref="NavCellFlags.Door"/> tag intact.
    /// </para>
    /// </summary>
    private static void InflateByBodyRadius(NavGrid grid, NavCellFlags[] passability, int bodyRadiusWu)
    {
        var inflateCells = (bodyRadiusWu + NavGrid.CellSizeWu - 1) / NavGrid.CellSizeWu;
        if (inflateCells <= 0)
        {
            return;
        }

        var sources = new List<(int X, int Y)>();
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                if (passability[grid.CellIndex(x, y)] == NavCellFlags.Blocked)
                {
                    sources.Add((x, y));
                }
            }
        }

        foreach (var (sourceX, sourceY) in sources)
        {
            var minX = Math.Max(0, sourceX - inflateCells);
            var maxX = Math.Min(grid.Width - 1, sourceX + inflateCells);
            var minY = Math.Max(0, sourceY - inflateCells);
            var maxY = Math.Min(grid.Height - 1, sourceY + inflateCells);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    passability[grid.CellIndex(x, y)] = NavCellFlags.Blocked;
                }
            }
        }
    }
}
