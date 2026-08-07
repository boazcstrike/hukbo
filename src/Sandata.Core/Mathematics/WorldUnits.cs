using Hukbo.Core.Mathematics;

namespace Sandata.Core.Mathematics;

/// <summary>
/// Converts a <see cref="FixedPoint"/> distance value into the plain
/// <see langword="long"/> world-unit integer that <c>NavGrid</c>,
/// <c>WallBuckets</c>, <c>LineOfSight</c>, <c>WeaponLoweredRules</c>, and
/// every rule built on them already take as a parameter. Design section 4 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c> stores distance as
/// a <see cref="FixedPoint"/> raw value at <see cref="FixedPoint.Scale"/>
/// 1024 — one world unit (wu) is one <see cref="FixedPoint"/> whole unit,
/// 1,024 raw. No conversion between the two existed before task 64 of
/// <c>docs/plans/2026-08-07-sandata-scaffold.md</c>; task 32 (the
/// weapon-lowered rule) needed exactly this bridge and reported that it did
/// not exist rather than inventing one out of scope. This type is that
/// bridge.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rounding rule, pinned.</b> C#'s <c>/</c> truncates toward zero, which
/// disagrees with an arithmetic right shift for a negative dividend — for
/// example <c>-1 / 1024 == 0</c> by truncating division, but
/// <c>-1 &gt;&gt; 10 == -1</c> by arithmetic shift, because the shift floors
/// toward negative infinity instead. Design section 4 requires explicit
/// floor division for every world-to-cell conversion, and
/// <c>NavGrid.WorldToCellCoordinate</c> already implements that requirement
/// by shifting rather than dividing:
/// <c>(int)(worldUnits &gt;&gt; CellSizeShiftAmount)</c>. This type shifts by
/// the same rule in the same direction, so a coordinate that passes through
/// both conversions floors consistently instead of disagreeing at a cell
/// boundary.
/// </para>
/// <para>
/// The map format itself cannot express a negative coordinate (design
/// section 12), so no floor-versus-truncate disagreement can reach baked map
/// data. A relative offset — for example the vector between two operators —
/// is signed, which is exactly why the floor rule still has to be written
/// down and pinned by a test on both sides of zero, rather than left to C#
/// division's truncate-toward-zero default.
/// </para>
/// </remarks>
public static class WorldUnits
{
    /// <summary>
    /// <c>log2(FixedPoint.Scale)</c>. Named rather than computed at every
    /// call site — matching <c>NavGrid.CellSizeShiftAmount</c>'s own
    /// convention — so a future change to <see cref="FixedPoint.Scale"/>
    /// that stops being a power of two is caught here rather than silently
    /// reintroducing a division.
    /// </summary>
    private const int RawToWorldUnitShiftAmount = 10; // 1 << 10 == 1024 == FixedPoint.Scale

    /// <summary>
    /// Converts <paramref name="value"/>'s raw representation into whole
    /// world units, flooring toward negative infinity. See this type's
    /// remarks for the exact rule and why it agrees with
    /// <c>NavGrid.WorldToCellCoordinate</c>.
    /// </summary>
    public static long FromFixedPoint(FixedPoint value) =>
        (long)value.RawValue >> RawToWorldUnitShiftAmount;
}
