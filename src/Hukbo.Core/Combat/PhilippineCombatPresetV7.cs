using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 7 of the pre-colonial Philippine combat preset: version 5's
/// nine-row roster, tables, ranks, and clash profile restated without
/// modification, plus a size-aware shield interception model and the narrow
/// breast-high shield it introduces.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph, matching the
/// convention every earlier preset version follows. Version 7 is a frozen
/// snapshot in the same sense versions 1 through 6 are: version 5's values
/// are restated here rather than referenced, so retuning version 7 can never
/// reach back and move a hash version 5's replays depend on. Versions 1
/// through 6 stay registered and unmodified.
/// <para>
/// <b>Version 7 descends from version 5, not version 6.</b> An earlier draft
/// of this file built forward from version 6 instead, which was wrong:
/// version 6 fields only four melee loadouts and no
/// <see cref="ShieldId.TallHardwood"/> roster entry at all, so a shield-size
/// feature built on top of it can never be exercised by a projectile, and the
/// shield-size-versus-projectile-size comparison this version exists to make
/// is structurally unobservable. Version 5 carries the three ranged
/// weapons — Bangkaw, Busog, and Arquebus — and the two
/// <see cref="ShieldId.TallHardwood"/> roster rows (Kalis and Itak), so it is
/// the only existing preset whose roster can actually field a projectile
/// against a tall shield. This version restates version 5's nine rows
/// verbatim and appends two new <see cref="ShieldId.NarrowBreastHigh"/> rows,
/// for eleven roster rows in total.
/// </para>
/// <para>
/// Why this version exists. <see cref="ShieldId.TallHardwood"/> intercepts a
/// flat basis-point share of every attack regardless of what struck it or
/// how physically large the shield is. That is a coarse model, and
/// docs/research/HISTORICAL_1500s_ARMOR.md section 6.4 documents at least
/// two shield sizes in the sixteenth-century record: body-length shields at
/// several points in the corpus, and Artieda's 1573 "breast-high, and little
/// more than half a <c>vara</c> wide" shield — roughly 42 centimeters, a
/// tall narrow shape. Version 7 adds <see cref="ShieldId.NarrowBreastHigh"/>
/// for that second shape and a size-aware interception formula,
/// <see cref="ClashProfile.ResolveShieldIntercept(ShieldId, WeaponId)"/>,
/// that weighs a shield's declared span against an attacking weapon's
/// declared shield-defeat bulk (<see cref="WeaponProfile.ShieldDefeatBulkRaw"/>).
/// </para>
/// <para>
/// The counter-intuitive tuning choice is the arquebus, which carries the
/// highest shield-defeat bulk of any weapon here despite being the
/// physically smallest. docs/research/HISTORICAL_1500s_ARMOR.md section 6.2
/// records the single most valuable sixteenth-century sentence on Philippine
/// shield construction, from Mactan, 27 April 1521: "the shots only passed
/// through the shields which were made of thin wood and the arms [of the
/// bearers]" — while the same account, in the same encounter, records
/// arrows stopped by the same shields ("they received them on their
/// shields"). The bulk table therefore ranks Arquebus far above Busog and
/// Bangkaw, modelling how completely each projectile defeats interception
/// rather than the weapon's physical size.
/// </para>
/// <para>
/// Every attribute value below is a PROVISIONAL gameplay tuning value, not a
/// historical measurement, and none of them may be cited back into
/// docs/research/HISTORICAL_1500s_WEAPONS.md or
/// docs/research/HISTORICAL_1500s_ARMOR.md. Weapon and shield names carry
/// evidence tiers recorded on <see cref="WeaponId"/> and <see cref="ShieldId"/>
/// and shown in the agent inspector.
/// </para>
/// <para>
/// The weapon-to-rank assignment below — Datu with the Kampilan, Maharlika
/// with the Wasay, Timawa with the Kalis, Itak, Bangkaw, Busog, and Arquebus,
/// Aliping Namamahay with the Itak — is carried over from version 5
/// unchanged, including for the two new
/// <see cref="ShieldId.NarrowBreastHigh"/> roster entries, which reuse the
/// weapon's existing shieldless-row rank rather than introduce a second
/// variable into the shield comparison. It is a provisional gameplay tuning
/// choice, not a historical claim. No sixteenth-century source assigns a
/// particular weapon to a particular social class, and
/// docs/research/HISTORICAL_1500s_RANKS.md is explicit that rank carries no
/// combat-strength value of its own.
/// </para>
/// <para>
/// Kalis and Itak are <see cref="WeaponGrip.OneHanded"/>, and
/// <see cref="CombatRuleset"/> requires a one-handed weapon to declare a
/// paired profile even when no roster entry ever resolves it. Version 7's
/// four <see cref="ShieldId.TallHardwood"/> and
/// <see cref="ShieldId.NarrowBreastHigh"/> roster entries make that paired
/// row reachable through <see cref="CombatRuleset.Roster"/>, without changing
/// the paired profile's own values.
/// </para>
/// <para>
/// All three ranged weapons are <see cref="WeaponGrip.TwoHanded"/> with
/// <see cref="ShieldId.None"/>, which satisfies <see cref="CombatRuleset"/>'s
/// existing two-handed/shieldless construction rule without any new guard
/// code, exactly as version 5 establishes.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV7
{
    public const int Version = 1;

    public static CombatRuleset Rules { get; } = Build();

    private static CombatRuleset Build()
    {
        var general = new TargetWeightProfile(
        [
            (BodyPart.WeaponArm, 10),
            (BodyPart.ShieldArm, 8),
            (BodyPart.Shoulder, 9),
            (BodyPart.Head, 9),
            (BodyPart.Neck, 9),
            (BodyPart.Face, 8),
            (BodyPart.Chest, 7),
            (BodyPart.Abdomen, 7),
            (BodyPart.Thigh, 8),
            (BodyPart.Knee, 7),
            (BodyPart.Shin, 7),
            (BodyPart.Hands, 8),
            (BodyPart.Feet, 2),
        ]);

        // Target weights, per weapon. The four melee rows and three ranged
        // rows are restated exactly from PhilippineCombatPresetV5. This
        // version changes shield interception only; where a blow lands is
        // untouched.
        var weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>
        {
            // Kampilan — Great Blade. A long single-edged blade reaching for
            // the head, neck, and shoulder line.
            [WeaponId.Kampilan] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Head] = 10,
                    [BodyPart.Neck] = 10,
                    [BodyPart.Shoulder] = 9,
                    [BodyPart.WeaponArm] = 8,
                    [BodyPart.ShieldArm] = 8,
                    [BodyPart.Chest] = 8,
                }),

            // Wasay — War Axe. A hafted head that comes down on the shoulder
            // and the arms.
            [WeaponId.Wasay] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Shoulder] = 10,
                    [BodyPart.Head] = 9,
                    [BodyPart.WeaponArm] = 9,
                    [BodyPart.ShieldArm] = 9,
                }),

            // Kalis — Thrusting Blade. A linear thrust into the trunk.
            [WeaponId.Kalis] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Abdomen] = 10,
                    [BodyPart.Chest] = 9,
                    [BodyPart.Neck] = 8,
                }),

            // Itak — Work Blade. A short blade working the near targets:
            // arms, hands, and whatever the guard leaves open.
            [WeaponId.Itak] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.WeaponArm] = 10,
                    [BodyPart.ShieldArm] = 10,
                    [BodyPart.Hands] = 9,
                    [BodyPart.Neck] = 8,
                    [BodyPart.Face] = 8,
                }),

            // Bangkaw — Thrown Spear. A thrown weapon aimed at the largest
            // available target, the torso, rather than a precise line.
            [WeaponId.Bangkaw] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 9,
                    [BodyPart.Shoulder] = 7,
                }),

            // Busog — Bow. A loosed arrow aimed center-mass, with a shallow
            // preference for the head at the archer's most careful shots.
            [WeaponId.Busog] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 8,
                    [BodyPart.Head] = 7,
                }),

            // Arquebus — Matchlock. A slow, heavy, imprecise shot aimed
            // broadly at the torso; the least discriminating of the seven.
            [WeaponId.Arquebus] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 9,
                    [BodyPart.Head] = 8,
                }),
        };

        // Weapon attributes. The four melee rows are restated exactly from
        // PhilippineCombatPresetV5, and the three ranged rows are restated
        // exactly from PhilippineCombatPresetV5's own RangedProfile calls.
        // This version changes shield interception only; damage, reach, and
        // cooldown are untouched.
        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 15,
                    reachWorldUnits: 16,
                    cooldownTicks: 7,
                    comboOpenChanceBasisPoints: 2_000,
                    comboContinueChanceBasisPoints: 3_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 4)),

            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 18,
                    reachWorldUnits: 13,
                    cooldownTicks: 8,
                    comboOpenChanceBasisPoints: 1_000,
                    comboContinueChanceBasisPoints: 2_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 5)),

            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 11,
                    reachWorldUnits: 13,
                    cooldownTicks: 5,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 3),
                Profile(
                    damage: 10,
                    reachWorldUnits: 12,
                    cooldownTicks: 5,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 3)),

            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 9,
                    reachWorldUnits: 11,
                    cooldownTicks: 4,
                    comboOpenChanceBasisPoints: 3_000,
                    comboContinueChanceBasisPoints: 4_000,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2),
                Profile(
                    damage: 8,
                    reachWorldUnits: 10,
                    cooldownTicks: 4,
                    comboOpenChanceBasisPoints: 3_000,
                    comboContinueChanceBasisPoints: 4_000,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2)),

            // Bangkaw — 3x Kampilan's 16-unit reach, restated exactly from
            // PhilippineCombatPresetV5.
            [WeaponId.Bangkaw] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 10,
                    reachWorldUnits: 48,
                    cooldownTicks: 25,
                    projectileSpeedWorldUnitsPerTick: 8,
                    standoffWorldUnits: 36,
                    flightTickCeiling: 10)),

            // Busog — 5x Kampilan's reach, restated exactly from
            // PhilippineCombatPresetV5.
            [WeaponId.Busog] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 8,
                    reachWorldUnits: 80,
                    cooldownTicks: 45,
                    projectileSpeedWorldUnitsPerTick: 14,
                    standoffWorldUnits: 60,
                    flightTickCeiling: 10)),

            // Arquebus — 7x Kampilan's reach, restated exactly from
            // PhilippineCombatPresetV5.
            [WeaponId.Arquebus] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 30,
                    reachWorldUnits: 112,
                    cooldownTicks: 240,
                    projectileSpeedWorldUnitsPerTick: 40,
                    standoffWorldUnits: 84,
                    flightTickCeiling: 6)),
        };

        var armors = new[] { ArmorId.LightOrganic };

        // ShieldId.None and ShieldId.TallHardwood restated exactly from
        // PhilippineCombatPresetV5. ShieldId.NarrowBreastHigh is new: its
        // defense multiplier is PROVISIONAL gameplay tuning, chosen weaker
        // than TallHardwood's 500 (between 500 and 1000, since a lower value
        // is a stronger reduction) to reflect a smaller shield covering the
        // chest and abdomen less completely, per
        // docs/research/HISTORICAL_1500s_ARMOR.md section 6.4 (Artieda,
        // 1573, "breast-high, and little more than half a vara wide").
        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = TargetWeightProfiles.FromMultiplierOverrides(
                new Dictionary<BodyPart, int>()),

            [ShieldId.TallHardwood] =
                TargetWeightProfiles.FromMultiplierOverrides(
                    new Dictionary<BodyPart, int>
                    {
                        [BodyPart.Chest] = 500,
                        [BodyPart.Abdomen] = 500,
                    }),

            [ShieldId.NarrowBreastHigh] =
                TargetWeightProfiles.FromMultiplierOverrides(
                    new Dictionary<BodyPart, int>
                    {
                        [BodyPart.Chest] = 750,
                        [BodyPart.Abdomen] = 750,
                    }),
        };

        // Roster order is part of the content-hash contract, indexed by
        // Scenario.RosterCounts. The nine ShieldId.None and
        // ShieldId.TallHardwood entries are restated verbatim from
        // PhilippineCombatPresetV5, including rank assignment. The two
        // ShieldId.NarrowBreastHigh entries are new, appended below in the
        // same shape PhilippineCombatPresetV5 uses for its
        // ShieldId.TallHardwood entries: each reuses the weapon's existing
        // shieldless-row rank so rank is not a second variable in the shield
        // comparison.
        var roster = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None, RankId.Maharlika),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, RankId.AlipingNamamahay),
            new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Busog, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Arquebus, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood, RankId.Timawa),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood, RankId.AlipingNamamahay),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh, RankId.Timawa),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh, RankId.AlipingNamamahay),
        };

        // Per-rank fighter levels, restated exactly from
        // PhilippineCombatPresetV5. Provisional gameplay tuning with no
        // evidentiary standing — see class remarks. Ayuey is declared even
        // though no roster entry fields it, because this is the game's one
        // canonical per-rank level table, not a table scoped to the roster.
        var rankLevels = new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Maharlika] = 2,
            [RankId.Timawa] = 2,
            [RankId.AlipingNamamahay] = 1,
            [RankId.Ayuey] = 1,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV7,
            Version,
            general,
            weaponTargets,
            armors,
            shieldMultipliers,
            roster,
            weaponAttributes: weaponAttributes,
            clashProfile: BuildClashProfile(),
            rankLevels: rankLevels);
    }

    /// <summary>
    /// PROVISIONAL defensive-interception tuning data. The sixty-three
    /// weapon-intercept cells, the nine void-channel cells, and the seven
    /// hard-share rows for <see cref="ShieldId.None"/> and
    /// <see cref="ShieldId.TallHardwood"/> defenders are restated exactly
    /// from <see cref="PhilippineCombatPresetV5"/>: this version changes how
    /// a shield's interception is computed, never how often a blow is thrown
    /// or how hard it lands. It adds the size-aware tables
    /// (<c>shieldInterceptBaseBasisPoints</c>, <c>shieldSpanRaw</c>,
    /// <c>shieldDefeatBulkRaw</c>) that gate
    /// <see cref="ClashProfile.DeclaresSizeAwareShieldIntercept"/> to
    /// <see langword="true"/>, plus new weapon-intercept and void-channel
    /// rows for the two new <see cref="ShieldId.NarrowBreastHigh"/> roster
    /// entries. Every one of the fourteen NarrowBreastHigh weapon-intercept
    /// cells is set below the corresponding
    /// <see cref="ShieldId.TallHardwood"/> row recorded in
    /// <see cref="PhilippineCombatPresetV5"/> for the same (defender weapon,
    /// attacker weapon) pair — a narrower shield intercepts less, before the
    /// size-aware formula is even applied. PROVISIONAL gameplay tuning
    /// throughout; see docs/research/WEAPON_CLASH_1500s.md,
    /// docs/research/HISTORICAL_1500s_ARMOR.md, and CLAUDE.md section 7.
    /// </summary>
    private static ClashProfile BuildClashProfile()
    {
        var weaponIntercept = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>
        {
            // Melee defender vs melee attacker — restated verbatim from
            // PhilippineCombatPresetV5 (itself restated from V4).
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan)] = 2_200,
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Wasay)] = 1_900,
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Kalis)] = 1_600,
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Itak)] = 2_000,

            [(WeaponId.Wasay, ShieldId.None, WeaponId.Kampilan)] = 1_500,
            [(WeaponId.Wasay, ShieldId.None, WeaponId.Wasay)] = 1_300,
            [(WeaponId.Wasay, ShieldId.None, WeaponId.Kalis)] = 1_100,
            [(WeaponId.Wasay, ShieldId.None, WeaponId.Itak)] = 1_400,

            [(WeaponId.Kalis, ShieldId.None, WeaponId.Kampilan)] = 1_200,
            [(WeaponId.Kalis, ShieldId.None, WeaponId.Wasay)] = 1_000,
            [(WeaponId.Kalis, ShieldId.None, WeaponId.Kalis)] = 1_500,
            [(WeaponId.Kalis, ShieldId.None, WeaponId.Itak)] = 1_500,

            [(WeaponId.Itak, ShieldId.None, WeaponId.Kampilan)] = 1_100,
            [(WeaponId.Itak, ShieldId.None, WeaponId.Wasay)] = 1_000,
            [(WeaponId.Itak, ShieldId.None, WeaponId.Kalis)] = 1_400,
            [(WeaponId.Itak, ShieldId.None, WeaponId.Itak)] = 1_400,

            // Melee defender vs ranged attacker — restated verbatim from
            // PhilippineCombatPresetV5.
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Bangkaw)] = 2_250,
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Busog)] = 1_750,
            [(WeaponId.Kampilan, ShieldId.None, WeaponId.Arquebus)] = 750,

            [(WeaponId.Wasay, ShieldId.None, WeaponId.Bangkaw)] = 2_000,
            [(WeaponId.Wasay, ShieldId.None, WeaponId.Busog)] = 1_500,
            [(WeaponId.Wasay, ShieldId.None, WeaponId.Arquebus)] = 625,

            [(WeaponId.Kalis, ShieldId.None, WeaponId.Bangkaw)] = 1_750,
            [(WeaponId.Kalis, ShieldId.None, WeaponId.Busog)] = 1_375,
            [(WeaponId.Kalis, ShieldId.None, WeaponId.Arquebus)] = 625,

            [(WeaponId.Itak, ShieldId.None, WeaponId.Bangkaw)] = 1_625,
            [(WeaponId.Itak, ShieldId.None, WeaponId.Busog)] = 1_250,
            [(WeaponId.Itak, ShieldId.None, WeaponId.Arquebus)] = 500,

            // Ranged defender vs any attacker — restated verbatim from
            // PhilippineCombatPresetV5.
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Kampilan)] = 1_500,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Wasay)] = 1_350,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Kalis)] = 1_350,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Itak)] = 1_350,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Bangkaw)] = 1_500,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Busog)] = 1_200,
            [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Arquebus)] = 450,

            [(WeaponId.Busog, ShieldId.None, WeaponId.Kampilan)] = 1_050,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Wasay)] = 900,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Kalis)] = 900,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Itak)] = 900,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Bangkaw)] = 1_050,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Busog)] = 1_050,
            [(WeaponId.Busog, ShieldId.None, WeaponId.Arquebus)] = 300,

            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Kampilan)] = 450,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Wasay)] = 375,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Kalis)] = 375,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Itak)] = 375,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Bangkaw)] = 450,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Busog)] = 375,
            [(WeaponId.Arquebus, ShieldId.None, WeaponId.Arquebus)] = 450,

            // ShieldId.TallHardwood defender keys, restated verbatim from
            // PhilippineCombatPresetV5. These are the values every
            // ShieldId.NarrowBreastHigh row below is set below.
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kampilan)] = 500,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Wasay)] = 400,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kalis)] = 600,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Itak)] = 600,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Bangkaw)] = 700,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Busog)] = 550,
            [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Arquebus)] = 250,

            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kampilan)] = 400,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Wasay)] = 300,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kalis)] = 500,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Itak)] = 500,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Bangkaw)] = 575,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Busog)] = 450,
            [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Arquebus)] = 175,

            // ShieldId.NarrowBreastHigh defender keys, new for V7. Every cell
            // below is set below its TallHardwood counterpart directly above
            // (~70% of the TallHardwood value, rounded to the nearest 25
            // basis points), reflecting a narrower shield turning less of the
            // blow before the size-aware formula is even applied.
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Kampilan)] = 350,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Wasay)] = 275,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Kalis)] = 425,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Itak)] = 425,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Bangkaw)] = 500,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Busog)] = 375,
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh, WeaponId.Arquebus)] = 175,

            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Kampilan)] = 275,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Wasay)] = 200,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Kalis)] = 350,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Itak)] = 350,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Bangkaw)] = 400,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Busog)] = 325,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh, WeaponId.Arquebus)] = 125,
        };

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>
        {
            // Restated verbatim from PhilippineCombatPresetV5.
            [(WeaponId.Kampilan, ShieldId.None)] = 1_000,
            [(WeaponId.Wasay, ShieldId.None)] = 900,
            [(WeaponId.Kalis, ShieldId.None)] = 1_350,
            [(WeaponId.Itak, ShieldId.None)] = 1_450,
            [(WeaponId.Bangkaw, ShieldId.None)] = 1_600,
            [(WeaponId.Busog, ShieldId.None)] = 1_700,
            [(WeaponId.Arquebus, ShieldId.None)] = 1_800,
            [(WeaponId.Kalis, ShieldId.TallHardwood)] = 1_000,
            [(WeaponId.Itak, ShieldId.TallHardwood)] = 1_100,

            // New for V7. PROVISIONAL: a warrior carrying the smaller,
            // lighter shield is modelled as relying a little more on
            // footwork to evade outright, so each row sits above its
            // ShieldId.TallHardwood counterpart for the same weapon. No test
            // constrains this exact value; only the [0, BasisPointScale]
            // range and roster coverage are required.
            [(WeaponId.Kalis, ShieldId.NarrowBreastHigh)] = 1_450,
            [(WeaponId.Itak, ShieldId.NarrowBreastHigh)] = 1_550,
        };

        // Restated verbatim from PhilippineCombatPresetV5: seven attacker
        // rows (four melee, three ranged).
        var hardShareBases = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 3_300,
            [WeaponId.Wasay] = 4_000,
            [WeaponId.Kalis] = 1_200,
            [WeaponId.Itak] = 1_800,
            [WeaponId.Bangkaw] = 2_200,
            [WeaponId.Busog] = 900,
            [WeaponId.Arquebus] = 4_500,
        };

        // Restated verbatim from PhilippineCombatPresetV5: seven defender
        // rows (four melee, three ranged).
        var hardShareMultipliers = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 1_150,
            [WeaponId.Wasay] = 1_050,
            [WeaponId.Kalis] = 750,
            [WeaponId.Itak] = 700,
            [WeaponId.Bangkaw] = 600,
            [WeaponId.Busog] = 400,
            [WeaponId.Arquebus] = 300,
        };

        // PROVISIONAL, size-aware shield interception, new for V7. Carried
        // over verbatim from this file's own prior draft: base basis points
        // and span per shield. TallHardwood's base of 2,400 is unchanged from
        // every prior version's flat shieldIntercept value, restated here so
        // a melee attack (zero shield-defeat bulk) against TallHardwood
        // resolves to the same 2,400 the flat formula always gave it — see
        // ShieldSizeInterceptionTests. NarrowBreastHigh's base of 1,700 and
        // both spans are PROVISIONAL tuning: span is world units multiplied
        // by FixedPoint.Scale, with TallHardwood modelled as twice
        // NarrowBreastHigh's span (12 world units against 6), loosely after
        // the "body-length" versus "roughly 42 centimeters" contrast in
        // docs/research/HISTORICAL_1500s_ARMOR.md section 6.4.
        var shieldInterceptBaseBasisPoints = new Dictionary<ShieldId, int>
        {
            [ShieldId.TallHardwood] = 2_400,
            [ShieldId.NarrowBreastHigh] = 1_700,
        };

        var shieldSpanRaw = new Dictionary<ShieldId, int>
        {
            [ShieldId.TallHardwood] = 12 * FixedPoint.Scale,
            [ShieldId.NarrowBreastHigh] = 6 * FixedPoint.Scale,
        };

        // PROVISIONAL, size-aware shield interception, new for V7. Per
        // attacking weapon shield-defeat bulk, as a raw fixed-point value.
        // Every melee weapon is zero: a melee blow strikes through the same
        // span a shield already covers and has no independent physical size
        // to compare against it. Values are ordered Busog < Bangkaw <
        // Arquebus, deliberately not ordered by physical projectile size:
        // see the class remarks on why the arquebus carries the highest bulk
        // here.
        var shieldDefeatBulkRaw = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 0,
            [WeaponId.Wasay] = 0,
            [WeaponId.Kalis] = 0,
            [WeaponId.Itak] = 0,
            [WeaponId.Busog] = 2 * FixedPoint.Scale,
            [WeaponId.Bangkaw] = 6 * FixedPoint.Scale,
            [WeaponId.Arquebus] = 30 * FixedPoint.Scale,
        };

        return new ClashProfile(
            weaponIntercept: weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: hardShareBases,
            hardShareMultipliers: hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500,
            shieldInterceptBaseBasisPoints: shieldInterceptBaseBasisPoints,
            shieldSpanRaw: shieldSpanRaw,
            shieldDefeatBulkRaw: shieldDefeatBulkRaw);
    }

    /// <summary>
    /// Authors a melee <see cref="WeaponProfile"/> from world units rather
    /// than raw fixed-point values, restated exactly from
    /// PhilippineCombatPresetV5's private helper of the same name and shape.
    /// </summary>
    private static WeaponProfile Profile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks,
        int comboOpenChanceBasisPoints,
        int comboContinueChanceBasisPoints,
        int comboMaxSteps,
        int comboCooldownTicks) =>
        new(
            damage,
            reachWorldUnits * FixedPoint.Scale,
            cooldownTicks,
            comboOpenChanceBasisPoints,
            comboContinueChanceBasisPoints,
            comboMaxSteps,
            comboCooldownTicks);

    /// <summary>
    /// Authors a ranged <see cref="WeaponProfile"/> from world units and
    /// world-units-per-tick rather than raw fixed-point values, restated
    /// exactly from PhilippineCombatPresetV5's private helper of the same
    /// name and shape. Melee combo chaining is disabled outright (zero open
    /// chance, matching PhilippineCombatPresetV2's zero-chance convention for
    /// a deliberate no-op) since none of the three ranged weapons this
    /// preset declares forms a combo chain.
    /// </summary>
    private static WeaponProfile RangedProfile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks,
        int projectileSpeedWorldUnitsPerTick,
        int standoffWorldUnits,
        int flightTickCeiling) =>
        new(
            damage,
            reachWorldUnits * FixedPoint.Scale,
            cooldownTicks,
            ComboOpenChanceBasisPoints: 0,
            ComboContinueChanceBasisPoints: 0,
            ComboMaxSteps: 1,
            ComboCooldownTicks: 1,
            ProjectileSpeedRaw: projectileSpeedWorldUnitsPerTick * FixedPoint.Scale,
            StandoffDistanceRaw: standoffWorldUnits * FixedPoint.Scale,
            FlightTickCeiling: flightTickCeiling);
}
