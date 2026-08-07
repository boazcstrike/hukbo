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

    /// <summary>
    /// Documented. Thrown role. Pigafetta, present and wounded at Mactan on 27
    /// April 1521, records bamboo spears — some iron-tipped — hurled at the
    /// captain-general and reused "four or six times". Pigafetta's 1521
    /// Visayan word list gives "for Spear — bancan", identified by Blair and
    /// Robertson's editors against <c>bangcao</c>; a zero-year gap between
    /// attestation and the depicted period. Visayan, Mindanao, and Maranao
    /// term — Tagalog is <c>sibat</c>, and the label must not be generalised
    /// across the archipelago.
    /// </summary>
    Bangkaw = 5,

    /// <summary>
    /// Documented. Pigafetta, Mactan, 27 April 1521: the captain-general shot
    /// through the leg with a poisoned arrow, and Pigafetta's own face wound
    /// days later at Cebu. Pigafetta's 1521 Visayan word list gives "for Bow
    /// — bossugh", identified against <c>bosog</c>, inherited from
    /// Proto-Austronesian *busuʀ* rather than borrowed; a zero-year gap
    /// between attestation and the depicted period.
    /// </summary>
    Busog = 6,

    /// <summary>
    /// Documented, form uncertain. Matchlock firearms in local hands are
    /// attested three separate times between roughly 1543 and 1567 — earliest
    /// García Descalante Alvarado, writing from Lisbon 7 August 1548 about the
    /// Sarangani and Mindanao area c. 1543-45 — and one specimen physically
    /// reached Spain via Legazpi in 1567. What is uncertain is the weapon's
    /// exact form and how many there were. "Arquebus" is a European term
    /// contemporary with the depicted period, not a cultural identification,
    /// so no Filipino name check applies.
    /// </summary>
    Arquebus = 7,
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

    /// <summary>
    /// V3 with a <see cref="RankId"/> assigned to each of the four solo
    /// roster entries and a per-rank fighter level replacing the placeholder
    /// level V3 uses uniformly. The weapon, attribute, and clash-profile
    /// tables are otherwise unchanged. V1, V2, and V3 stay registered and
    /// unmodified so their replays remain reproducible.
    /// </summary>
    PrecolonialPhilippinesV4 = 4,

    /// <summary>
    /// V4 plus three ranged roster entries — Bangkaw, Busog, and Arquebus,
    /// each <see cref="WeaponGrip.TwoHanded"/> with <see cref="ShieldId.None"/>
    /// — carrying the ranged-only <see cref="WeaponProfile"/> fields
    /// (projectile speed, standoff distance, flight-tick ceiling). V4's four
    /// melee rows and its shared target-weight profile are otherwise
    /// unchanged. V1 through V4 stay registered and unmodified so their
    /// replays remain reproducible. Every ranged value V5 carries is a
    /// provisional gameplay-tuning reconstruction under CLAUDE.md section 7,
    /// never a historical measurement.
    /// </summary>
    PrecolonialPhilippinesV5 = 5,
}

/// <summary>
/// Social and legal standing in a sixteenth-century Philippine society, as
/// documented in <c>docs/research/HISTORICAL_1500s_RANKS.md</c>. This is
/// never a delegated military office — no source attests a graded rank
/// structure below a chief's personal following (see
/// <c>docs/research/ARMY-COMPOSITION.md</c> §11.4) — and it carries no
/// combat-strength value of its own; the research document is explicit that
/// no sixteenth-century source grades fighting ability by social class.
/// Numeric values are part of the deterministic replay and content-hash
/// contract; do not renumber or reorder.
/// </summary>
public enum RankId
{
    /// <summary>
    /// Documented. Datu — Chief. Tagalog and Visayan. The best-attested
    /// term, recorded by Plasencia in 1589, verbatim, including the war
    /// obligation owed to a chief by those below him. The descriptor
    /// deliberately avoids "noble", which carries Plasencia's Spanish gloss
    /// rather than its content, and avoids the modern "royalty" misreading
    /// entirely.
    /// </summary>
    Datu = 1,

    /// <summary>
    /// Documented. Maharlika — Sworn Freeman. Tagalog. Plasencia, 1589:
    /// free-born, exempt from tribute, but obligated to accompany the datu
    /// in war at their own expense, in exchange for a feast beforehand and a
    /// share of the spoils afterward. The modern popularised reading of
    /// "maharlika" as nobility or royalty is a twentieth-century development
    /// that Plasencia's own text does not support and this project does not
    /// use.
    /// </summary>
    Maharlika = 2,

    /// <summary>
    /// Documented. Timawa — Bound Freeman. Visayan. Loarca, 1582, spelled
    /// <c>timagua</c>: a freeman sworn to a chosen chief, obligated to carry
    /// weapons and accompany him, in exchange for the chief's reciprocal
    /// obligation to defend him. The bond was transferable — a timawa was
    /// free to leave one chief's service for another's. Must not be used for
    /// the Tagalog region: Morga's <c>timagua</c> means plebeian commoner, a
    /// different sense from Loarca's sworn fighting client, and the two are
    /// not interchangeable.
    /// </summary>
    Timawa = 3,

    /// <summary>
    /// Documented. Aliping Namamahay — Householder. Tagalog. Plasencia,
    /// 1589: married dependents who held their own houses, land, and gold,
    /// and could not be sold or moved from their village. Plasencia gives
    /// this class maritime and agricultural service and no armed obligation
    /// of its own; whether a householder was ever put in a battle line is an
    /// inference either way, not an attested fact, and any roster that
    /// fields this class must say so wherever the class is shown.
    /// </summary>
    AlipingNamamahay = 4,

    /// <summary>
    /// Documented, form uncertain. Ayuey — Household Dependent. Visayan.
    /// Loarca, 1582: the most dependent of his three recorded grades — fed
    /// and clothed by the master, working three days in four for him. The
    /// institution is attested; the spelling is Loarca's own transcription
    /// and its modern orthography is unsettled. Declared here but not
    /// rostered by any preset, on the same principle preset V3 already uses
    /// for its unreachable paired weapon profiles.
    /// </summary>
    Ayuey = 5,
}

/// <summary>
/// Authoritative weapon, armor, shield, and rank identity assigned to one
/// warrior. This value is part of authoritative simulation state and the
/// state hash.
/// </summary>
/// <remarks>
/// <paramref name="Rank"/> defaults to <see cref="RankId.Timawa"/> so every
/// one of the many existing three-argument construction sites keeps
/// resolving to a single, uniform rank. That uniformity is also the reason
/// presets that do not declare rank levels (V1 through V3) can fold nothing
/// rank-related into their content or state hash: every warrior they field
/// already carries the same rank value, so a rank fold would be a constant,
/// and gating on preset declaration rather than on this default is what
/// keeps their pinned hashes unchanged.
/// </remarks>
public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield,
    RankId Rank = RankId.Timawa);
