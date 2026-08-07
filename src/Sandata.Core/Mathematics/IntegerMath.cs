namespace Sandata.Core.Mathematics;

/// <summary>
/// Integer division helpers that floor toward negative infinity rather than
/// truncating toward zero. C#'s built-in <c>/</c> and <c>%</c> truncate
/// toward zero — <c>-3 / 2 == -1</c>, not <c>-2</c> — which is the correct
/// contract for arithmetic but the wrong one for world-to-grid-cell
/// conversion: it makes cell <c>-1</c> and cell <c>0</c> both claim the
/// coordinate range immediately around the origin, merging two grid cells
/// into one exactly at the point every map's coordinate system is anchored.
/// <see cref="FloorDiv"/> and <see cref="FloorMod"/> give the grid the
/// conversion it actually needs.
/// </summary>
public static class IntegerMath
{
    /// <summary>
    /// The largest integer quotient not exceeding the exact mathematical
    /// value of <paramref name="value"/> divided by <paramref name="divisor"/>
    /// — floor division, not truncating division.
    ///
    /// <para>
    /// Derived from <see cref="FloorMod"/> by <c>(value - FloorMod(value, divisor)) / divisor</c>.
    /// Because <see cref="FloorMod"/> always returns the exact remainder of
    /// the floor decomposition, <c>value - FloorMod(value, divisor)</c> is
    /// always an exact multiple of <paramref name="divisor"/>, so the final
    /// <c>/</c> divides evenly and truncating versus flooring makes no
    /// difference to it — the flooring work is entirely inside
    /// <see cref="FloorMod"/>, and this method adds no branch of its own.
    /// </para>
    /// </summary>
    /// <exception cref="DivideByZeroException"><paramref name="divisor"/> is zero.</exception>
    public static int FloorDiv(int value, int divisor) => (value - FloorMod(value, divisor)) / divisor;

    /// <summary>
    /// The remainder of floor division: a value with the same sign as
    /// <paramref name="divisor"/> (or zero), unlike C#'s <c>%</c>, which
    /// takes the sign of <paramref name="value"/> (or zero).
    ///
    /// <para>
    /// Branchless. <c>value % divisor</c> already has the correct magnitude;
    /// it only needs correcting when it is nonzero and its sign disagrees
    /// with <paramref name="divisor"/>'s, in which case adding
    /// <paramref name="divisor"/> once is exactly enough to flip it onto the
    /// divisor's sign. Both conditions are folded into all-ones-or-all-zeros
    /// bitmasks instead of an <c>if</c>:
    /// </para>
    /// <para>
    /// <c>isNonZero</c> is <c>-1</c> when the remainder is nonzero and
    /// <c>0</c> when it is zero, computed as
    /// <c>(remainder | -remainder) &gt;&gt; 31</c> — for any nonzero integer,
    /// at least one of the value and its negation has its sign bit set, so
    /// their bitwise OR is negative; for zero, both are zero, so the OR is
    /// zero and the arithmetic right shift produces all zero bits.
    /// </para>
    /// <para>
    /// <c>signsDiffer</c> is <c>-1</c> when <paramref name="divisor"/> and
    /// the remainder have different sign bits and <c>0</c> when they match,
    /// computed as <c>(remainder ^ divisor) &gt;&gt; 31</c> — XOR's sign bit
    /// is set exactly when the two operands' sign bits differ, and the
    /// arithmetic shift spreads that one bit across the whole word.
    /// </para>
    /// <para>
    /// The two masks are combined with <c>&amp;</c> so the correction applies
    /// only when both conditions hold, then <c>mask &amp; divisor</c> is
    /// either <paramref name="divisor"/> (mask all-ones) or <c>0</c> (mask
    /// all-zeros), added to the truncating remainder. Zero remainder is
    /// therefore never corrected regardless of <paramref name="divisor"/>'s
    /// sign, which a naïve <c>(remainder ^ divisor) &gt;&gt; 31</c>-only mask
    /// would get wrong for an exact multiple with a negative divisor.
    /// </para>
    /// </summary>
    /// <exception cref="DivideByZeroException"><paramref name="divisor"/> is zero.</exception>
    public static int FloorMod(int value, int divisor)
    {
        var remainder = value % divisor;
        var isNonZero = (remainder | -remainder) >> 31;
        var signsDiffer = (remainder ^ divisor) >> 31;
        var correction = isNonZero & signsDiffer;
        return remainder + (correction & divisor);
    }
}
