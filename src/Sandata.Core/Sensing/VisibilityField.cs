namespace Sandata.Core.Sensing;

/// <summary>
/// A reusable, flat, per-cell visibility buffer over a
/// <see cref="Navigation.NavGrid"/>-shaped grid: one boolean per cell, indexed
/// by the same flat cell index as every other per-node array in this
/// codebase, exactly the shape design section 7's "The ported algorithms"
/// paragraph of docs/plans/2026-08-07-sandata-scaffold-design.md means by
/// "the per-faction fog-of-war cell visibility layer that sits behind the
/// per-unit cone" — this type is that layer, and <see cref="Shadowcast"/> is
/// what fills it in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, never hashed, never snapshotted.</b> Design section 4, "What
/// is derived and never hashed, never snapshotted", lists "line-of-sight
/// results and vision-cone membership for the current tick" among the values
/// that are rebuilt from authoritative state rather than carried forward. A
/// visibility field is exactly that kind of value: it is a pure function of
/// the nav grid's current passability, an origin cell, and a radius, so it is
/// always safe to recompute it from scratch and never safe to fold it into a
/// save file, the state hash, or the event hash.
/// </para>
/// <para>
/// <b>Allocated once, cleared per query.</b> This type owns exactly one
/// <c>bool[]</c>, sized once at construction, and <see cref="Clear"/> resets
/// that same array in place rather than allocating a new one. The intended
/// call pattern for a caller running one query per faction per tick is:
/// construct one <see cref="VisibilityField"/> per faction up front, then each
/// tick call <see cref="Clear"/> followed by one or more
/// <see cref="Shadowcast.Compute"/> calls into the same instance — nothing in
/// that hot path allocates.
/// </para>
/// </remarks>
public sealed class VisibilityField
{
    private readonly bool[] _visible;

    /// <summary>
    /// The number of cells this field covers. Always equal to the
    /// <see cref="Navigation.NavGrid.CellCount"/> of the grid this field was
    /// constructed to cover.
    /// </summary>
    public int CellCount => _visible.Length;

    /// <summary>
    /// Allocates a visibility field covering <paramref name="cellCount"/>
    /// cells, every cell initially not visible.
    /// </summary>
    /// <param name="cellCount">
    /// The number of cells to cover. In normal use this is a
    /// <see cref="Navigation.NavGrid"/>'s <c>CellCount</c>, but this type does
    /// not itself hold a reference to a grid, so any non-negative length is
    /// accepted.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="cellCount"/> is negative.
    /// </exception>
    public VisibilityField(int cellCount)
    {
        if (cellCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellCount), cellCount, "Cell count cannot be negative.");
        }

        _visible = new bool[cellCount];
    }

    /// <summary>
    /// Whether <paramref name="cellIndex"/> is currently marked visible.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="cellIndex"/> is outside <c>[0, CellCount)</c>.
    /// </exception>
    public bool IsVisible(int cellIndex) => _visible[cellIndex];

    /// <summary>
    /// Marks <paramref name="cellIndex"/> visible. Idempotent: marking an
    /// already-visible cell visible again has no further effect.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="cellIndex"/> is outside <c>[0, CellCount)</c>.
    /// </exception>
    public void MarkVisible(int cellIndex) => _visible[cellIndex] = true;

    /// <summary>
    /// Resets every cell to not visible, in place, without allocating a new
    /// backing array. A caller reusing this instance for a fresh query — a
    /// new origin, a new radius, or simply the next tick — calls this first.
    /// </summary>
    public void Clear() => Array.Clear(_visible);
}
