namespace Sandata.Core.Weapons;

/// <summary>
/// The fire modes a firearm supports, combined as flags. Design section 9
/// records five distinct sets across the 38-row roster —
/// <c>Safe | Single | Auto</c> (eighteen rifles), <c>Safe | Single | Burst3</c>
/// (M16A4, M4), <c>Safe | Single | Burst2 | Auto</c> (AK-12 2018/2021, G36),
/// <c>Single</c> alone (striker-fired pistols with no manual safety), and
/// <c>Safe | Single</c> (the remaining pistols) — and the fire-mode band
/// selection rule in the same section tests <see cref="Burst3"/> before
/// <see cref="Burst2"/> so a weapon that carried both would still resolve
/// deterministically. No row in the current roster carries both.
/// </summary>
/// <remarks>
/// Every flag is a distinct power of two so combinations compose without
/// collision, matching the same replay-contract rule
/// <see cref="FirearmId"/>'s remarks describe: append-only, a flag's value
/// never changes once it has shipped, and a new mode is added at the next
/// free power of two, never by reusing or reordering an existing one.
/// </remarks>
[Flags]
public enum FireModeSet
{
    /// <summary>Safe: the weapon will not fire. Not itself a firing mode, but part of the selector's position set.</summary>
    Safe = 1,

    /// <summary>Single, semi-automatic fire: one round per trigger pull.</summary>
    Single = 2,

    /// <summary>A mechanically baked two-round burst.</summary>
    Burst2 = 4,

    /// <summary>A mechanically baked three-round burst.</summary>
    Burst3 = 8,

    /// <summary>Fully automatic fire.</summary>
    Auto = 16,
}
