namespace Sandata.Core.Navigation;

/// <summary>
/// The two-pass integer chamfer distance transform design section 7 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md calls "the clearance
/// field": for every cell of a <see cref="NavGrid"/>-shaped grid, the
/// weighted grid distance to the nearest blocked cell.
///
/// <para>
/// <b>Why the weights are <c>(10, 14)</c>, and not any other pair.</b> This
/// type does not declare its own step costs; it reuses
/// <see cref="NavHeuristic.OrthogonalStepCost"/> (10) and
/// <see cref="NavHeuristic.DiagonalStepCost"/> (14) — the exact weights the
/// A* octile heuristic uses for a cardinal and a diagonal step. That is
/// deliberate, not incidental: a clearance value and a formation
/// half-width are both then expressed in the same unit as an A* path cost,
/// so the doorway collapse in design section 8 — "when the clearance under
/// a group's leader drops below the formation half-width, every slot's
/// lateral offset goes to zero" — is a direct integer comparison of two
/// values already in the same scale, never a conversion between two
/// different distance systems.
/// </para>
///
/// <para>
/// <b>What the transform computes.</b> A blocked cell (a zero byte in the
/// <c>passability</c> input — see the caller-supplied convention documented
/// on <see cref="Build"/>) has clearance zero. Every other cell's clearance
/// is the length of the cheapest path to some blocked cell, where an
/// orthogonal step costs 10 and a diagonal step costs 14, exactly as
/// <see cref="NavHeuristic.Octile"/> prices the same two step kinds. This
/// two-pass raster propagation is the classical Rosenfeld-Pfaltz / Borgefors
/// chamfer distance transform: it is exact for this metric (not an
/// approximation of it), because every cell's true cheapest path can be
/// decomposed into a monotonically-increasing-then-monotonically-decreasing
/// walk that one forward sweep and one backward sweep, each relaxing
/// against the four neighbours already visited in that sweep's direction,
/// is guaranteed to find.
/// </para>
///
/// <para>
/// <b>Single-threaded, one fixed scan order.</b> Design section 4: "Single-
/// threaded authoritative schedule. No parallel flood fill, ever: visit
/// order becomes thread-schedule-dependent and that is a guaranteed
/// desync." Pass one visits every cell top-left to bottom-right (row-major,
/// increasing <c>y</c> then increasing <c>x</c>) and relaxes against the
/// west, north, north-west, and north-east neighbours — the four neighbours
/// that sweep has already visited by the time it reaches the current cell.
/// Pass two visits every cell in exactly the reverse order and relaxes
/// against the remaining four: east, south, south-east, south-west. Neither
/// pass ever spawns a thread, a <c>Task</c>, or a <c>Parallel.For</c>.
/// </para>
/// </summary>
public static class ClearanceField
{
    /// <summary>
    /// The clearance value stored for a blocked cell. Never produced by
    /// relaxation — the smallest clearance an open cell can ever relax to is
    /// <see cref="NavHeuristic.OrthogonalStepCost"/>, one step above zero —
    /// so a caller can always tell a blocked cell's clearance from an open
    /// one's by comparing against this constant alone.
    /// </summary>
    public const int BlockedClearance = 0;

    /// <summary>
    /// The sentinel an open cell holds before any relaxation reaches it.
    /// Set far enough from <see cref="int.MaxValue"/> that adding
    /// <see cref="NavHeuristic.DiagonalStepCost"/> to it, repeatedly, across
    /// the largest grid <see cref="NavGrid.MaxDimensionCells"/> allows, can
    /// never overflow. A cell that still holds this value once both passes
    /// finish had no blocked cell reachable in <c>passability</c> at all —
    /// an input this type accepts (nothing about the transform requires a
    /// blocked cell to exist) but that a caller should treat as "no
    /// meaningful clearance limit" rather than as a real distance.
    /// </summary>
    private const int Unreached = int.MaxValue / 2;

    /// <summary>
    /// Builds the clearance field for the whole grid, from scratch.
    ///
    /// <para>
    /// <paramref name="passability"/> is read under one convention only, the
    /// one this type owns end to end: byte value <c>0</c> means blocked,
    /// every other byte value means open. That is narrower than
    /// design section 7's own three-valued <c>passability</c> array
    /// (<c>0</c> blocked, <c>1</c> open, <c>2</c> door — passable to the
    /// planner, impassable to the mover until opened): this type has no way
    /// to know, and does not need to know, which of the two non-zero values
    /// a caller's byte carries, because a chamfer distance to "the nearest
    /// blocked cell" only ever cares about the blocked/not-blocked
    /// distinction. Task 18 (<c>NavBake</c>) owns translating its richer
    /// byte encoding into this one, and a later task wires the two
    /// together; this type takes the already-reduced form as an explicit
    /// parameter precisely so it never has to reach into
    /// <see cref="NavGrid"/> to find out.
    /// </para>
    /// </summary>
    /// <param name="passability">
    /// One byte per cell, length <c>width * height</c>, indexed by
    /// <c>(y * width) + x</c> exactly as <see cref="NavGrid.CellIndex"/>
    /// does. Zero means blocked; any other value means open.
    /// </param>
    /// <param name="clearance">
    /// The output buffer, length <c>width * height</c>, indexed the same
    /// way. Every element is overwritten; the caller's prior contents are
    /// never read.
    /// </param>
    /// <param name="width">The grid width in cells. Must be positive.</param>
    /// <param name="height">The grid height in cells. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="passability"/> or <paramref name="clearance"/> does
    /// not have exactly <c>width * height</c> elements.
    /// </exception>
    public static void Build(ReadOnlySpan<byte> passability, Span<int> clearance, int width, int height)
    {
        ValidateGridShape(passability.Length, clearance.Length, width, height);

        Seed(passability, clearance);

        SweepTopLeftToBottomRight(clearance, width, height, minX: 0, minY: 0, maxX: width - 1, maxY: height - 1);
        SweepBottomRightToTopLeft(clearance, width, height, minX: 0, minY: 0, maxX: width - 1, maxY: height - 1);
    }

    /// <summary>
    /// Rebuilds the clearance field for only the cells that a door opening
    /// or closing could possibly have affected, rather than every cell in
    /// the grid — design section 7: "A door change rebakes only the cells
    /// within the chamfer's radius of influence of the changed cells, which
    /// is bounded".
    ///
    /// <para>
    /// <b>What "bounded" means here, precisely.</b> The recompute window is
    /// the changed cells' bounding rectangle, expanded by
    /// <paramref name="radiusOfInfluenceCells"/> cells on every side and
    /// clamped to the grid, exactly as if the caller had asked "rebuild
    /// only the cells within this many cells of the change; leave the
    /// stored value of everything farther away untouched." Inside that
    /// window every cell — blocked or open — is reset from
    /// <paramref name="passability"/> and re-relaxed by the same two sweeps
    /// <see cref="Build"/> runs, restricted to the window; a sweep reading a
    /// neighbour outside the window reads that neighbour's existing,
    /// unmodified <paramref name="clearance"/> entry, so a value already
    /// known to be correct from farther away still propagates in at the
    /// window's edge exactly as it would in a full rebuild. Nothing outside
    /// the window is written.
    /// </para>
    ///
    /// <para>
    /// The caller carries the responsibility <see cref="Build"/> does not
    /// have to: choosing <paramref name="radiusOfInfluenceCells"/> large
    /// enough that no cell outside the window could have had its true
    /// nearest-blocked-cell distance move because of the change. A door is a
    /// local edit next to other, unaffected geometry — the wall it sits in,
    /// the room around it — so in practice that radius is small and
    /// bounded, not a function of the grid's size. Choosing it too small is
    /// not a determinism hazard by itself: the recompute always runs the
    /// same two fixed sweeps in the same fixed order, so the result for a
    /// given input is exactly reproducible — but a radius that is too small
    /// silently leaves some cell holding a value that has drifted from what
    /// a full <see cref="Build"/> would produce for the same
    /// <paramref name="passability"/>, which is exactly the "an incremental
    /// update that drifts from the full recompute is a silent desync"
    /// failure this type exists to avoid. There is no field on this type
    /// that can catch that mistake after the fact; only a comparison against
    /// a full rebuild, as the golden-replay-shaped test in
    /// <c>ClearanceFieldTests</c> runs, can.
    /// </para>
    /// </summary>
    /// <param name="passability">
    /// The grid's current passability, already reflecting whatever door
    /// change is being applied. Same shape and convention as
    /// <see cref="Build"/>'s parameter of the same name.
    /// </param>
    /// <param name="clearance">
    /// The field being rebuilt in place. On entry it must already hold the
    /// clearance field for the passability that was in effect before the
    /// door change — the result of an earlier <see cref="Build"/> or
    /// <see cref="RebuildLocal"/> call against the same grid. Only the cells
    /// inside the recompute window are overwritten.
    /// </param>
    /// <param name="width">The grid width in cells. Must be positive.</param>
    /// <param name="height">The grid height in cells. Must be positive.</param>
    /// <param name="changedMinX">The changed rectangle's smallest X, inclusive.</param>
    /// <param name="changedMinY">The changed rectangle's smallest Y, inclusive.</param>
    /// <param name="changedMaxX">The changed rectangle's largest X, inclusive.</param>
    /// <param name="changedMaxY">The changed rectangle's largest Y, inclusive.</param>
    /// <param name="radiusOfInfluenceCells">
    /// How many cells beyond the changed rectangle, on every side, to
    /// include in the recompute window. Must not be negative.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive;
    /// <paramref name="radiusOfInfluenceCells"/> is negative; or the changed
    /// rectangle is empty, inverted, or outside the grid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="passability"/> or <paramref name="clearance"/> does
    /// not have exactly <c>width * height</c> elements.
    /// </exception>
    public static void RebuildLocal(
        ReadOnlySpan<byte> passability,
        Span<int> clearance,
        int width,
        int height,
        int changedMinX,
        int changedMinY,
        int changedMaxX,
        int changedMaxY,
        int radiusOfInfluenceCells)
    {
        ValidateGridShape(passability.Length, clearance.Length, width, height);

        if (radiusOfInfluenceCells < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusOfInfluenceCells),
                radiusOfInfluenceCells,
                "The radius of influence cannot be negative.");
        }

        if (changedMinX < 0 || changedMinY < 0 || changedMaxX >= width || changedMaxY >= height
            || changedMinX > changedMaxX || changedMinY > changedMaxY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(changedMaxX),
                (changedMinX, changedMinY, changedMaxX, changedMaxY),
                $"The changed rectangle must be non-empty and inside the {width} by {height} grid.");
        }

        var windowMinX = Math.Max(0, changedMinX - radiusOfInfluenceCells);
        var windowMinY = Math.Max(0, changedMinY - radiusOfInfluenceCells);
        var windowMaxX = Math.Min(width - 1, changedMaxX + radiusOfInfluenceCells);
        var windowMaxY = Math.Min(height - 1, changedMaxY + radiusOfInfluenceCells);

        SeedWindow(passability, clearance, width, windowMinX, windowMinY, windowMaxX, windowMaxY);

        SweepTopLeftToBottomRight(clearance, width, height, windowMinX, windowMinY, windowMaxX, windowMaxY);
        SweepBottomRightToTopLeft(clearance, width, height, windowMinX, windowMinY, windowMaxX, windowMaxY);
    }

    private static void ValidateGridShape(int passabilityLength, int clearanceLength, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
        }

        var expectedLength = checked(width * height);

        if (passabilityLength != expectedLength)
        {
            throw new ArgumentException(
                $"passability must have exactly {expectedLength} elements for a {width} by {height} grid, but had {passabilityLength}.",
                nameof(passabilityLength));
        }

        if (clearanceLength != expectedLength)
        {
            throw new ArgumentException(
                $"clearance must have exactly {expectedLength} elements for a {width} by {height} grid, but had {clearanceLength}.",
                nameof(clearanceLength));
        }
    }

    /// <summary>
    /// Resets every cell of <paramref name="clearance"/> from
    /// <paramref name="passability"/>: <see cref="BlockedClearance"/> for a
    /// blocked cell, <see cref="Unreached"/> for an open one, awaiting the
    /// two sweeps.
    /// </summary>
    private static void Seed(ReadOnlySpan<byte> passability, Span<int> clearance)
    {
        for (var index = 0; index < clearance.Length; index++)
        {
            clearance[index] = passability[index] == 0 ? BlockedClearance : Unreached;
        }
    }

    /// <summary>
    /// The windowed form of <see cref="Seed"/>: resets only the cells inside
    /// the inclusive rectangle <c>[minX, maxX] x [minY, maxY]</c>, leaving
    /// every cell outside it exactly as the caller passed it in.
    /// </summary>
    private static void SeedWindow(
        ReadOnlySpan<byte> passability, Span<int> clearance, int width, int minX, int minY, int maxX, int maxY)
    {
        for (var y = minY; y <= maxY; y++)
        {
            var rowStart = y * width;

            for (var x = minX; x <= maxX; x++)
            {
                var index = rowStart + x;
                clearance[index] = passability[index] == 0 ? BlockedClearance : Unreached;
            }
        }
    }

    /// <summary>
    /// Pass one. Visits every cell of the inclusive rectangle
    /// <c>[minX, maxX] x [minY, maxY]</c> in row-major order — increasing
    /// <c>y</c>, then increasing <c>x</c> within a row — and relaxes it
    /// against the west, north, north-west, and north-east neighbours: the
    /// four neighbours this scan order has already visited by the time it
    /// reaches the current cell. A neighbour outside <c>[minX, maxX] x
    /// [minY, maxY]</c> — including one outside the grid entirely — is read
    /// from <paramref name="clearance"/> exactly as it already stands,
    /// which is how a full <see cref="Build"/> and a windowed
    /// <see cref="RebuildLocal"/> use the very same sweep: <see cref="Build"/>
    /// happens to pass a window equal to the whole grid, so "outside the
    /// window" and "outside the grid" coincide there.
    /// </summary>
    private static void SweepTopLeftToBottomRight(
        Span<int> clearance, int width, int height, int minX, int minY, int maxX, int maxY)
    {
        for (var y = minY; y <= maxY; y++)
        {
            var rowStart = y * width;

            for (var x = minX; x <= maxX; x++)
            {
                var index = rowStart + x;

                if (clearance[index] == BlockedClearance)
                {
                    continue;
                }

                RelaxFrom(clearance, width, height, index, x - 1, y, NavHeuristic.OrthogonalStepCost); // west
                RelaxFrom(clearance, width, height, index, x, y - 1, NavHeuristic.OrthogonalStepCost); // north
                RelaxFrom(clearance, width, height, index, x - 1, y - 1, NavHeuristic.DiagonalStepCost); // north-west
                RelaxFrom(clearance, width, height, index, x + 1, y - 1, NavHeuristic.DiagonalStepCost); // north-east
            }
        }
    }

    /// <summary>
    /// Pass two, the mirror image of <see cref="SweepTopLeftToBottomRight"/>:
    /// visits the same inclusive rectangle in exactly the reverse order —
    /// decreasing <c>y</c>, then decreasing <c>x</c> within a row — and
    /// relaxes against the remaining four neighbours: east, south,
    /// south-east, and south-west.
    /// </summary>
    private static void SweepBottomRightToTopLeft(
        Span<int> clearance, int width, int height, int minX, int minY, int maxX, int maxY)
    {
        for (var y = maxY; y >= minY; y--)
        {
            var rowStart = y * width;

            for (var x = maxX; x >= minX; x--)
            {
                var index = rowStart + x;

                if (clearance[index] == BlockedClearance)
                {
                    continue;
                }

                RelaxFrom(clearance, width, height, index, x + 1, y, NavHeuristic.OrthogonalStepCost); // east
                RelaxFrom(clearance, width, height, index, x, y + 1, NavHeuristic.OrthogonalStepCost); // south
                RelaxFrom(clearance, width, height, index, x + 1, y + 1, NavHeuristic.DiagonalStepCost); // south-east
                RelaxFrom(clearance, width, height, index, x - 1, y + 1, NavHeuristic.DiagonalStepCost); // south-west
            }
        }
    }

    /// <summary>
    /// If cell <c>(neighbourX, neighbourY)</c> is inside the grid, and its
    /// clearance plus <paramref name="stepCost"/> is smaller than
    /// <paramref name="clearance"/><c>[cellIndex]</c>'s current value,
    /// lowers <paramref name="clearance"/><c>[cellIndex]</c> to that sum.
    /// Does nothing when the neighbour is out of bounds or the candidate is
    /// not an improvement. <paramref name="stepCost"/> is
    /// <see cref="Unreached"/>-safe: the sentinel plus
    /// <see cref="NavHeuristic.DiagonalStepCost"/>, the larger of the two
    /// step costs, stays far below <see cref="int.MaxValue"/>.
    /// </summary>
    private static void RelaxFrom(
        Span<int> clearance, int width, int height, int cellIndex, int neighbourX, int neighbourY, int stepCost)
    {
        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height)
        {
            return;
        }

        var candidate = clearance[(neighbourY * width) + neighbourX] + stepCost;

        if (candidate < clearance[cellIndex])
        {
            clearance[cellIndex] = candidate;
        }
    }
}
