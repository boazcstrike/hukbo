namespace Sandata.Core.Weapons;

/// <summary>
/// The four mechanism families that select a weapon's mechanism sound layer
/// — the selector detent, the bolt, the button thunk — on top of the
/// <see cref="CaliberFamily"/> report. Design section 10: the Steyr AUG has
/// no rotary selector, only a cross-bolt push-button safety, so its
/// mode-change sound must not share the AK or AR selector sample, which is
/// exactly why <see cref="Bullpup"/> is its own group rather than folded into
/// <see cref="Ar"/>.
/// </summary>
/// <remarks>
/// Exactly four members. Every numeric value is part of the same replay
/// contract <see cref="FirearmId"/>'s remarks describe: append-only, never
/// reordered, never renumbered without a new preset version, since
/// <c>FirearmCatalog</c>'s content hash folds this value for every row.
/// </remarks>
public enum MechanismGroup
{
    /// <summary>AK-pattern mechanism.</summary>
    Ak = 0,

    /// <summary>AR-pattern mechanism.</summary>
    Ar = 1,

    /// <summary>Bullpup mechanism: a push-button or cross-bolt safety rather than a rotary selector.</summary>
    Bullpup = 2,

    /// <summary>Pistol mechanism.</summary>
    Pistol = 3,
}
