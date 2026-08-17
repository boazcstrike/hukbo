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
/// <remarks>
/// This enum is append-only. A new baseline takes the next free value and
/// every existing value keeps the one it has, because a recorded replay
/// stores the numeric value rather than the name. Renumbering a member, or
/// removing one, silently rewrites the meaning of every replay and content
/// hash that already mentions it.
/// <para>
/// <see cref="LightOrganic"/> is retained deliberately and is deliberately
/// unused by new presets. Every preset up to and including
/// <see cref="CombatPresetId.PrecolonialPhilippinesV6"/> fields it on every
/// roster row, so it can never be retired; new presets field
/// <see cref="None"/> instead, which avoids a second generic value competing
/// with it for the same meaning.
/// </para>
/// </remarks>
public enum ArmorId
{
    /// <summary>
    /// Legacy. Retained unchanged so the replays of every preset up to and
    /// including <see cref="CombatPresetId.PrecolonialPhilippinesV6"/> keep
    /// reproducing. New presets do not field it.
    /// </summary>
    LightOrganic = 1,

    /// <summary>
    /// No body armor. A bare torso, documented at Mactan in 1521.
    /// </summary>
    None = 2,

    /// <summary>
    /// A layered plant-fibre or cotton garment.
    /// </summary>
    QuiltedCotton = 3,

    /// <summary>
    /// A corselet of carabao or buffalo hide.
    /// </summary>
    HideCorselet = 4,

    /// <summary>
    /// A rigid plate of hardwood, horn, or bamboo worn over the torso.
    /// </summary>
    RigidCuirass = 5,

    /// <summary>
    /// Imported iron. Rare, and historically contested — see
    /// docs/research/HISTORICAL_1500s_ARMOR.md section 4.6.
    /// </summary>
    ImportedIron = 6,
}

/// <summary>
/// How much a warrior's armor encumbers them. Armor affects movement through
/// this three-value class rather than through a per-armor movement row,
/// because the canonical loadout key is
/// <c>(WeaponId, ArmorId, ShieldId)</c> and giving armor its own row for each
/// baseline would multiply that hand-authored table roughly sixfold.
/// </summary>
/// <remarks>
/// The class is a gameplay grouping, not a historical measurement. No source
/// in the period states what any armor weighed or how much it slowed its
/// wearer.
/// </remarks>
public enum ArmorWeightClass
{
    /// <summary>
    /// Flexible organic armor, or none at all. No movement penalty.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Hide. A small movement penalty.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// A rigid plate of hardwood, horn, bamboo, or iron. The largest movement
    /// penalty this model applies.
    /// </summary>
    Rigid = 3,
}

/// <summary>
/// Maps every <see cref="ArmorId"/> to exactly one
/// <see cref="ArmorWeightClass"/>.
/// </summary>
/// <remarks>
/// The mapping is total by construction: <see cref="Resolve"/> throws for an
/// unmapped value rather than returning a default, so a future armor fails
/// loudly here instead of silently inheriting the light class and its absent
/// movement penalty. That is the same forward-compatibility guard
/// <c>MovementRuleset.CanonicalLoadoutIndex</c> and
/// <c>LoadoutCompositionCounts.Add</c> already apply to the loadout triple.
/// </remarks>
public static class ArmorWeightClasses
{
    /// <summary>
    /// Resolves one armor identity's weight class.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="armor"/> is not a declared
    /// <see cref="ArmorId"/> member.
    /// </exception>
    public static ArmorWeightClass Resolve(ArmorId armor) =>
        armor switch
        {
            ArmorId.LightOrganic => ArmorWeightClass.Light,
            ArmorId.None => ArmorWeightClass.Light,
            ArmorId.QuiltedCotton => ArmorWeightClass.Light,
            ArmorId.HideCorselet => ArmorWeightClass.Medium,
            ArmorId.RigidCuirass => ArmorWeightClass.Rigid,
            ArmorId.ImportedIron => ArmorWeightClass.Rigid,
            _ => throw new ArgumentOutOfRangeException(
                nameof(armor),
                armor,
                "Every ArmorId must declare a weight class."),
        };

    /// <summary>
    /// Basis points of a warrior's unencumbered movement speed left after
    /// their armor's weight class is applied, where 10,000 is 100 per cent.
    /// </summary>
    /// <remarks>
    /// Provisional gameplay tuning under CLAUDE.md section 7, not a historical
    /// measurement. No source in the period states what any armor weighed or
    /// how much it slowed its wearer.
    /// <para>
    /// The penalties are deliberately small. Damage reduction is meant to be
    /// armor's effect; the movement cost exists so the choice has a price a
    /// spectator can see in the inspector, not so it decides battles. A large
    /// penalty here would also compound with the mitigation to stall
    /// engagements, which is the failure mode the armor identity design's
    /// section 6.1 warns against by name.
    /// </para>
    /// </remarks>
    public static int PaceBasisPoints(ArmorWeightClass weightClass) =>
        weightClass switch
        {
            ArmorWeightClass.Light => 10_000,
            ArmorWeightClass.Medium => 9_700,
            ArmorWeightClass.Rigid => 9_400,
            _ => throw new ArgumentOutOfRangeException(
                nameof(weightClass),
                weightClass,
                "Every ArmorWeightClass must declare a pace."),
        };

    /// <summary>
    /// Scales an unencumbered movement speed by the pace its armor allows.
    /// </summary>
    /// <remarks>
    /// This is applied once per warrior at spawn, in
    /// <c>BattleSimulation.CreateAgent</c>, rather than through a
    /// <c>LoadoutMovementProfile</c> row. The profile rows are empty for every
    /// preset whose <c>MovementRuleset.UsesEquipmentRelativeFootwork</c> is
    /// false, which includes every preset from V10 onward and therefore the
    /// one the client ships, so a pace modifier hung there would never be read
    /// by the running game.
    /// <para>
    /// It is applied unconditionally and is still a no-op for every combat
    /// preset up to and including version 6, because all six field
    /// <see cref="ArmorId.LightOrganic"/> on every roster row,
    /// <see cref="ArmorId.LightOrganic"/> resolves to
    /// <see cref="ArmorWeightClass.Light"/>, and Light resolves to a flat
    /// 10,000 basis points. Their agents therefore spawn with a byte-identical
    /// movement speed and their recorded replays keep reproducing. That is why
    /// this needs no gate of its own.
    /// </para>
    /// <para>
    /// Integer arithmetic throughout, in the same basis-point convention
    /// <c>LoadoutMovementProfile</c> already uses, because the result reaches
    /// the per-tick state hash.
    /// </para>
    /// </remarks>
    public static int ScaleMovementSpeed(int movementSpeedRaw, ArmorId armor)
    {
        var pace = PaceBasisPoints(Resolve(armor));
        if (pace == 10_000)
        {
            return movementSpeedRaw;
        }

        return (int)((long)movementSpeedRaw * pace / 10_000);
    }
}

/// <summary>
/// Stable shield identity. Numeric values are part of the deterministic
/// replay and content-hash contract; do not renumber or reorder.
/// </summary>
public enum ShieldId
{
    None = 1,
    TallHardwood = 2,

    /// <summary>
    /// Documented, form uncertain. Artieda, 1573, describes shields "breast-
    /// high, and little more than half a <c>vara</c> wide" — roughly 42
    /// centimeters, a tall narrow shape rather than the body-length shields
    /// recorded elsewhere in the same period. See
    /// docs/research/HISTORICAL_1500s_ARMOR.md section 6.1 (summary table row
    /// "Breast-high shield, little more than half a <c>vara</c> wide") and
    /// section 6.4 "Dimensions". No source gives this shield shape a Filipino
    /// name, so none is invented for it here — the identifier stays the plain
    /// English descriptor per CLAUDE.md section 7.
    /// </summary>
    NarrowBreastHigh = 3,
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

    /// <summary>
    /// V4's tables, roster, ranks, and clash profile restated without
    /// modification, with the attack cooldown, combo cooldown, and damage of
    /// all six melee loadouts retuned so that blows land roughly half as often
    /// and hurt roughly twice as much. Damage per tick is held within two per
    /// cent of V4's for every loadout, so time to kill is effectively
    /// unchanged while the on-screen artefact rate — event-feed lines and
    /// clash crosses — roughly halves. It exists to answer the CL-1, CL-3, and
    /// CL-7 legibility failures recorded in
    /// docs/development/smoke-checklist.md on 2026-08-11; the reasoning is in
    /// the combat cadence V6 design. Descends from V4
    /// rather than V5, so it fields the same four melee weapons and no ranged
    /// row. V1 through V5 stay registered and unmodified so their replays
    /// remain reproducible. Every value V6 retunes is a provisional
    /// gameplay-tuning value under CLAUDE.md section 7, never a historical
    /// measurement.
    /// </summary>
    PrecolonialPhilippinesV6 = 6,

    /// <summary>
    /// V6's roster, tables, and clash profile restated without modification,
    /// plus a size-aware shield interception model. Adds
    /// <see cref="ShieldId.NarrowBreastHigh"/>, roster entries pairing it with
    /// Kalis and Itak, a per-shield base intercept and span, and a per-weapon
    /// shield-defeat bulk (<see cref="WeaponProfile.ShieldDefeatBulkRaw"/>) so
    /// a wide-bulk projectile such as an arquebus ball defeats a shield's
    /// interception more than a melee blow does, and defeats a narrow shield
    /// more than a broad one — see
    /// docs/research/HISTORICAL_1500s_ARMOR.md section 6.2, Mactan 1521:
    /// "the shots only passed through the shields which were made of thin
    /// wood and the arms [of the bearers]". V1 through V6 stay registered and
    /// unmodified so their replays remain reproducible. Every value V7
    /// introduces is a provisional gameplay-tuning value under CLAUDE.md
    /// section 7, never a historical measurement.
    /// </summary>
    PrecolonialPhilippinesV7 = 7,

    /// <summary>
    /// V7's roster, tables, ranks, weapon attributes, size-aware shield
    /// interception, and clash profile restated without modification, with
    /// armor made authoritative: a per-<c>(ArmorId, BodyPart)</c>
    /// damage-mitigation table in basis points, and a roster that fields five
    /// armor baselines rather than repeating
    /// <see cref="ArmorId.LightOrganic"/> on every row, giving fifty-five
    /// entries. Descends from V7 for the same reason V7 descends from V5
    /// rather than V6: taking the newest preset as the parent is what keeps
    /// the shipped build cumulative, and descending from anything older would
    /// have silently dropped the narrow breast-high shield and its size-aware
    /// interception from the player's build. V1 through V7 stay registered and
    /// unmodified so their replays remain reproducible, and their content
    /// hashes are unmoved because the mitigation table is folded only by a
    /// preset that declares one. Every mitigation value is a provisional
    /// gameplay-tuning reconstruction under CLAUDE.md section 7, never a
    /// historical measurement — no source in the period states what any
    /// armor stopped. See the armor identity design.
    /// </summary>
    PrecolonialPhilippinesV8 = 8,
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
