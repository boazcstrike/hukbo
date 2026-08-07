namespace Sandata.Core.Weapons;

/// <summary>
/// Converts an authored millisecond timing into an integer tick count, using
/// the one conversion rule design section 4 defines for v0.1 —
/// <see cref="Sandata.Core.Rules.MsToTickConversionRule.HalfAwayFromZero"/>.
/// This is the single place that formula is written; every weapon timing in
/// the roster (<c>ReadyMs</c>, <c>AimBaseMs</c>, <c>AimPerBamMs</c>,
/// <c>ResetMs</c>, <c>ReloadMs</c>) is meant to fold through
/// <see cref="ToTicks"/> at ruleset bake time rather than repeating the
/// arithmetic at each call site.
/// </summary>
/// <remarks>
/// <b>Hash contract — do not change this formula without a new preset
/// version.</b> <c>ticks = (milliseconds * tickRate + 500) / 1000</c>, using
/// the CLR's integer division, which truncates toward zero. For every
/// non-negative <paramref name="milliseconds"/> and positive
/// <paramref name="tickRate"/> — the only domain this project ever bakes —
/// adding half of the divisor before truncating is exactly "round to
/// nearest, ties away from zero": a value that lands precisely on the
/// boundary between two tick counts (for example 10 ms at 50 Hz, which is
/// exactly half a tick) rounds up rather than down. Baked once at ruleset
/// load and never recomputed per tick, so this method's cost is irrelevant
/// to the per-tick budget; its correctness and stability are what matter,
/// per <c>CLAUDE.md</c> section 5 and design section 4.
/// </remarks>
public static class TickConversion
{
    /// <summary>
    /// Converts <paramref name="milliseconds"/> to an integer tick count at
    /// <paramref name="tickRate"/> ticks per second, by the pinned formula
    /// documented on this type.
    /// </summary>
    /// <param name="milliseconds">
    /// An authored timing in milliseconds. Never negative for any value this
    /// project bakes.
    /// </param>
    /// <param name="tickRate">
    /// Ticks per second, <see cref="Sandata.Core.Rules.SandataRuleset.TickRate"/>.
    /// Must be positive.
    /// </param>
    /// <returns>The equivalent whole number of ticks, never negative.</returns>
    public static int ToTicks(int milliseconds, int tickRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickRate, 0);

        // Widened to long before the multiply so a large millisecond value
        // (well beyond anything the 38-weapon roster authors) cannot
        // overflow a 32-bit product ahead of the pinned formula's division.
        var scaledPlusHalf = ((long)milliseconds * tickRate) + 500;
        return (int)(scaledPlusHalf / 1000);
    }
}
