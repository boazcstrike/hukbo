namespace Hukbo.Core.Combat;

/// <summary>
/// Stable weapon identity. Numeric values are part of the deterministic
/// replay and content-hash contract; do not renumber or reorder.
/// </summary>
/// <remarks>
/// Symbols carry the Filipino name; the player-facing label is the pair form
/// (the name, an em dash, and a plain English descriptor) built in the client.
/// Renaming a symbol is hash-neutral because the numeric value is the hashed
/// quantity. The evidence tier behind each name is presentation metadata and
/// lives in the client; see docs/research/HISTORICAL_1500s_WEAPONS.md.
/// </remarks>
public enum WeaponId
{
    /// <summary>
    /// Documented, form uncertain. Pigafetta records a large cutting sword at
    /// Mactan in 1521 and gives it no local name; <c>kampilan</c> is the name
    /// later tradition attaches to this blade class.
    /// </summary>
    Kampilan = 1,

    /// <summary>
    /// Documented, form uncertain. A hafted battle axe with a broad metal
    /// head. Chosen over <c>panabas</c>, whose first documented mentions are
    /// nineteenth-century — a gap the pair-form policy in CLAUDE.md section 7
    /// refuses to badge as PROVISIONAL.
    /// </summary>
    Wasay = 2,

    /// <summary>
    /// Documented. Pigafetta recorded <c>calis</c> in the Visayas in 1521 and
    /// the term recurs across vocabularies from 1612 onward. The
    /// best-attested of the four.
    /// </summary>
    Kalis = 3,

    /// <summary>
    /// Provisional reconstruction. A Tagalog term for a field and utility
    /// blade also used in fighting. Preferred over the former enum identity
    /// "Bolo", a Spanish-era term the research document warns against using
    /// as a blanket name.
    /// </summary>
    Itak = 4,
}

/// <summary>
/// How many hands a weapon occupies. Static configuration: never drawn from,
/// never written to agent state. Numeric values are part of the deterministic
/// content-hash contract; do not renumber or reorder.
/// </summary>
public enum WeaponGrip
{
    /// <summary>
    /// Occupies both hands. A shield is forbidden, not merely absent, and
    /// <see cref="CombatRuleset"/> throws at construction for a roster entry
    /// that pairs one with a shield.
    /// </summary>
    TwoHanded = 1,

    /// <summary>
    /// May be carried alone or paired with a shield, and declares one
    /// attribute profile for each.
    /// </summary>
    OneHanded = 2,
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

    /// <summary>
    /// V1 plus per-weapon damage, reach, and attack cooldown split by grip,
    /// and a six-entry roster fielding a solo and a paired loadout for each
    /// one-handed weapon. V1 stays registered and unmodified so its replays
    /// remain reproducible.
    /// </summary>
    PrecolonialPhilippinesV2 = 2,

    /// <summary>
    /// V2 minus the two paired loadouts, plus attack combinations: an
    /// opening roll on a landed blow, a continuation roll on each following
    /// blow, a maximum chain length driven by a placeholder fighter level,
    /// and a faster cooldown while a chain is active. Fields only the four
    /// solo loadouts — Kampilan, Wasay, solo Kalis, solo Itak. V1 and V2 stay
    /// registered and unmodified so their replays remain reproducible.
    /// </summary>
    PrecolonialPhilippinesV3 = 3,
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
