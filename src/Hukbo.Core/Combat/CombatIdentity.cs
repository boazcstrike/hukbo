namespace Hukbo.Core.Combat;

/// <summary>
/// Stable weapon identity. Numeric values are part of the deterministic
/// replay and content-hash contract; do not renumber or reorder.
/// </summary>
public enum WeaponId
{
    GreatBlade = 1,
    HeavyChopper = 2,
    ThrustingBlade = 3,

    /// <summary>
    /// Enum identity only. The player-facing display name is the plain
    /// descriptor "Work Blade", not this identifier: "Bolo" is a local and
    /// Spanish-era term, and CLAUDE.md SS7 confines cultural identifications
    /// like it to evidence metadata, never an unqualified UI label. See the
    /// PROVISIONAL note in docs/research/HISTORICAL_1500s_WEAPONS.md.
    /// </summary>
    Bolo = 4,
}

/// <summary>
/// Stable armor identity. Numeric values are part of the deterministic
/// replay and content-hash contract; do not renumber or reorder.
/// </summary>
public enum ArmorId
{
    LightOrganic = 1,
}

/// <summary>
/// Stable shield identity. Numeric values are part of the deterministic
/// replay and content-hash contract; do not renumber or reorder.
/// </summary>
public enum ShieldId
{
    None = 1,
    TallHardwood = 2,
}

/// <summary>
/// Stable combat preset identity. Numeric values are part of the
/// deterministic replay and content-hash contract; do not renumber or
/// reorder. A new ruleset requires a new value plus a new
/// <see cref="CombatPresetRegistry"/> entry.
/// </summary>
public enum CombatPresetId
{
    PrecolonialPhilippinesV1 = 1,
}

/// <summary>
/// Authoritative weapon, armor, and shield identity assigned to one
/// warrior. This value is part of authoritative simulation state and the
/// state hash.
/// </summary>
public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield);
