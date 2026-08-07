namespace Sandata.Client.Audio;

/// <summary>
/// The eight sound families the Sandata catalog declares. Unlike
/// <c>Hukbo.Client.Audio.GameSoundId</c>, which is one enum member per
/// finished melee sound, a Sandata family is a category whose individual
/// identity is carried by <see cref="SoundSlot.FamilyKey"/>,
/// <see cref="SoundSlot.Mode"/>, and <see cref="SoundSlot.Environment"/>
/// instead of by a fresh enum member per combination. Design section 10
/// records why: gunfire has no hit location, the axis Hukbo's melee catalog
/// keys its variants on, so expressing weapon by fire mode by environment
/// under that shape would need one enum member per combination, which is
/// unacceptable bloat at 484 files. See
/// docs/research/2026-08-07-sandata-research-consolidated.md section 6 for
/// the full matrix this reduces.
/// </summary>
/// <remarks>
/// Numeric values follow the same append-only convention as
/// <c>Sandata.Core.Weapons.CaliberFamily</c>: never reordered, never
/// renumbered. A new family is added at the next free ordinal.
/// </remarks>
internal enum SoundFamily
{
    /// <summary>A single gunshot report, keyed by caliber family.</summary>
    GunReport = 0,

    /// <summary>The looping body of sustained automatic fire.</summary>
    GunLoop = 1,

    /// <summary>The tail that follows a <see cref="GunLoop"/> instance.</summary>
    GunTail = 2,

    /// <summary>A mechanism sound: selector, magazine, or bolt.</summary>
    Mechanism = 3,

    /// <summary>A dry-fire click with no round chambered.</summary>
    Dry = 4,

    /// <summary>A bullet striking a surface.</summary>
    Impact = 5,

    /// <summary>A spent casing landing on a surface.</summary>
    Casing = 6,

    /// <summary>A user-interface cue, reserved for future Sandata UI slots.</summary>
    Ui = 7,
}
