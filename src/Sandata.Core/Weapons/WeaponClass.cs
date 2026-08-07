namespace Sandata.Core.Weapons;

/// <summary>
/// The broad handling class of a firearm. Shotguns, submachine guns, and
/// light machine guns are out of scope for v0.1 by instruction — design
/// section 9 and docs/research/2026-08-07-sandata-research-consolidated.md
/// section 3 — so only the two classes the 38-row roster actually uses are
/// declared.
/// </summary>
/// <remarks>
/// Every numeric value is part of the same replay contract
/// <see cref="FirearmId"/>'s remarks describe: append-only, never reordered,
/// never renumbered without a new preset version.
/// </remarks>
public enum WeaponClass
{
    /// <summary>A shoulder-fired rifle, carbine, or bullpup.</summary>
    Rifle = 0,

    /// <summary>A handgun.</summary>
    Pistol = 1,
}
