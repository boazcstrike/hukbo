using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 5 pre-colonial Philippine combat preset: version 4's four melee
/// loadouts restated verbatim, plus three ranged loadouts — Bangkaw (thrown
/// spear), Busog (bow), and Arquebus (matchlock firearm).
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph, matching the
/// convention every earlier preset version follows. Version 5 is a frozen
/// snapshot in the same sense versions 1 through 4 are: version 4's melee
/// values are restated here rather than referenced, so retuning version 5 can
/// never reach back and move a hash version 4's replays depend on.
/// <para>
/// Every attribute value below is a provisional gameplay tuning value, not a
/// historical measurement, and none of them may be cited back into
/// docs/research/HISTORICAL_1500s_WEAPONS.md. Weapon names carry evidence
/// tiers recorded on <see cref="WeaponId"/> and shown in the agent inspector.
/// </para>
/// <para>
/// <b>The three ranged rows are entirely new, provisional gameplay tuning —
/// RU-24 owns calibrating them, not this task.</b> Reach is authored as a
/// multiple of the longest melee reach (Kampilan's 16 world units): 3x for
/// Bangkaw, 5x for Busog, 7x for Arquebus. Shot intervals preserve the
/// ordering Bangkaw &lt; Busog &lt;&lt; Arquebus — a firearm that fires rarely,
/// loudly, and for high single-shot damage rather than steady attrition.
/// Standoff distance sits at roughly three-quarters of each weapon's own
/// reach, strictly inside it, satisfying the construction-time check
/// <see cref="WeaponProfile"/> already enforces.
/// </para>
/// <para>
/// The ranged-units design document, section 2, is explicit that no
/// source consulted associates the bow, the thrown spear, or the arquebus
/// with any social rank, and that inventing one would not be supported by the
/// record. All three ranged rows therefore carry <see cref="RankId.Timawa"/>
/// — the <see cref="CombatLoadout"/> default — uniformly, rather than
/// inventing a distinct rank per weapon the way the four melee rows do.
/// </para>
/// <para>
/// All three ranged weapons are <see cref="WeaponGrip.TwoHanded"/> with
/// <see cref="ShieldId.None"/>, which satisfies
/// <see cref="CombatRuleset"/>'s existing two-handed/shieldless construction
/// rule without any new guard code.
/// </para>
/// <para>
/// Kalis and Itak are <see cref="WeaponGrip.OneHanded"/>, and
/// <see cref="CombatRuleset"/> requires a one-handed weapon to declare a
/// paired profile even when no roster entry ever resolves it — a missing
/// paired profile is an error, not a silent fallback to the solo one. Version
/// 4 fielded only the solo loadout for each, leaving the paired rows below
/// reachable only through the construction invariant. RU-45 adds one
/// <see cref="ShieldId.TallHardwood"/> roster entry for each of Kalis and
/// Itak — same rank as each weapon's existing shieldless row, so rank is not
/// a second variable — which makes the paired rows below reachable through
/// <see cref="CombatRuleset.Roster"/> for the first time in this preset.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV5
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

        // Target weights, per weapon. The four melee rows are restated
        // verbatim from PhilippineCombatPresetV4; the three ranged rows are
        // new, provisional gameplay tuning favoring center-mass hits, the way
        // an aimed shot rather than a swung blade would.
        var weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>
        {
            // Kampilan — Great Blade. A long single-edged blade aimed at the
            // head, neck, and shoulder line.
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

            // Wasay — War Axe. A hafted, broad-headed axe favoring the
            // shoulder and the limb that carries the blow.
            [WeaponId.Wasay] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Shoulder] = 10,
                    [BodyPart.Head] = 9,
                    [BodyPart.WeaponArm] = 9,
                    [BodyPart.ShieldArm] = 9,
                }),

            // Kalis — Wave Blade. A thrusting-capable blade favoring the
            // torso.
            [WeaponId.Kalis] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Abdomen] = 10,
                    [BodyPart.Chest] = 9,
                    [BodyPart.Neck] = 8,
                }),

            // Itak — Field Blade. A utility blade favoring the limbs and the
            // hands that carry a shield or a weapon.
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

        // Damage/reach/cooldown and the four combo fields restated exactly
        // from PhilippineCombatPresetV4 for the four melee weapons (both
        // grips, for the two one-handed weapons). The three ranged rows are
        // new: RangedProfile appends the three ranged fields RU-07 added to
        // WeaponProfile, and disables melee combo chains (open chance zero,
        // matching the zero-chance convention PhilippineCombatPresetV2
        // already uses for a deliberate no-op).
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

            // Bangkaw — 3x Kampilan's 16-unit reach. Fastest shot interval of
            // the three ranged weapons: a thrown spear is quick to recover
            // and re-throw relative to drawing a bow or reloading a matchlock.
            [WeaponId.Bangkaw] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 10,
                    reachWorldUnits: 48,
                    cooldownTicks: 25,
                    projectileSpeedWorldUnitsPerTick: 8,
                    standoffWorldUnits: 36,
                    flightTickCeiling: 10)),

            // Busog — 5x Kampilan's reach. Slower to loose than a thrown
            // spear but faster than reloading a matchlock; a lighter,
            // faster-flying projectile than the spear.
            [WeaponId.Busog] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 8,
                    reachWorldUnits: 80,
                    cooldownTicks: 45,
                    projectileSpeedWorldUnitsPerTick: 14,
                    standoffWorldUnits: 60,
                    flightTickCeiling: 10)),

            // Arquebus — 7x Kampilan's reach. Dramatically slower shot
            // interval than either the spear or the bow — a rare, loud, high
            // single-shot-damage weapon rather than a source of steady
            // attrition — with the fastest projectile of the three.
            [WeaponId.Arquebus] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 30,
                    reachWorldUnits: 112,
                    cooldownTicks: 240,
                    projectileSpeedWorldUnitsPerTick: 40,
                    standoffWorldUnits: 84,
                    flightTickCeiling: 6)),
        };

        // Armor and shield data restated exactly from PhilippineCombatPresetV4.
        var armors = new[] { ArmorId.LightOrganic };

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
        };

        // Content-hash-affecting per Scenario.RosterCounts. The four melee
        // entries are restated verbatim from PhilippineCombatPresetV4,
        // including their RankId assignment. The three ranged entries are
        // new roster data — see the class remarks on why all three share
        // RankId.Timawa rather than each inventing a distinct rank.
        //
        // RU-45: two ShieldId.TallHardwood entries appended below, one each
        // for Kalis and Itak, so band (b) of the ranged calibration ("shielded
        // roster entries absorb more blows before dying than shieldless
        // ones") is measurable at all -- every prior V5 entry carried
        // ShieldId.None. Each new entry reuses its weapon's existing
        // shieldless-row RankId so rank is not a second variable in that
        // comparison; see PhilippineCombatIntegrationTests.cs:793-800 for why
        // that pairing matters. Existing seven entries and their order are
        // untouched.
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
        };

        // Per-rank fighter levels restated exactly from PhilippineCombatPresetV4.
        // Provisional gameplay tuning with no evidentiary standing whatsoever
        // — see class remarks. Ayuey is declared even though no roster entry
        // fields it, on the same principle PhilippineCombatPresetV4 already
        // establishes for its unreachable paired weapon profiles: this is the
        // game's one canonical per-rank level table, not scoped to this
        // preset's roster alone.
        var rankLevels = new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Maharlika] = 2,
            [RankId.Timawa] = 2,
            [RankId.AlipingNamamahay] = 1,
            [RankId.Ayuey] = 1,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV5,
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
    /// Defensive-interception tuning data for version 5's nine-entry roster
    /// (seven distinct defender weapon/shield keys — two of the nine entries,
    /// Kalis and Itak, each field a second key under
    /// <see cref="ShieldId.TallHardwood"/>). <see cref="CombatRuleset"/>'s
    /// <c>ValidateClashProfileCoversTheRoster</c> requires, for every distinct
    /// defender key, a weapon-intercept cell against each of the roster's
    /// seven distinct attacker weapons plus one void/hard-share row, so this
    /// restates version 4's sixteen melee cells verbatim, adds every cell a
    /// ranged-weapon-including roster requires, and — as of RU-45 — adds the
    /// fourteen weapon-intercept cells and two void cells the two
    /// <see cref="ShieldId.TallHardwood"/> defender keys require.
    /// </summary>
    /// <remarks>
    /// PROVISIONAL. Every value here is a gameplay tuning choice, not a
    /// historical measurement — see docs/research/WEAPON_CLASH_1500s.md and
    /// CLAUDE.md section 7. The melee-vs-melee sixteen cells are restated
    /// verbatim from PhilippineCombatPresetV4. The new cells reason from one
    /// premise only: a parry or an evasive step is a close-quarters skill, so
    /// a defender never trained to fight at arm's length with a spear, a bow,
    /// or a firearm intercepts a blow — melee or ranged — markedly worse than
    /// a defender carrying a blade does, and a ranged attacker's own strike is
    /// harder for any defender to intercept than a bladed one because it
    /// arrives from beyond weapon's reach. The RU-45 shielded cells reason
    /// from a second premise: <see cref="ShieldId.TallHardwood"/> intercepts
    /// through a separate, structural channel
    /// (<see cref="ClashProfile.ResolveShieldIntercept"/>, unchanged at
    /// <c>2_400</c> basis points below), so the shielded weapon-intercept
    /// cells are deliberately lower per cell than their shieldless
    /// counterparts — the shield channel makes up the rest, and the combined
    /// total is what should exceed the shieldless total. Values follow
    /// PhilippineCombatPresetV2's exact precedent (V2:277-286): where V2's
    /// four legacy melee columns exist, its ratios reused directly (Kalis at
    /// ~40% of its shieldless cell, Itak at ~30-36%); the three ranged
    /// columns V2 never had are new, scaled by that same weapon's ratio
    /// applied to this preset's own shieldless ranged cells above and rounded
    /// to the nearest 25 basis points.
    /// </remarks>
    private static ClashProfile BuildClashProfile()
    {
        var weaponIntercept =
            new Dictionary<(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>
            {
                // Melee defender vs melee attacker — restated verbatim from
                // PhilippineCombatPresetV4.
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

                // Melee defender vs ranged attacker — new. A blade-armed
                // defender is somewhat worse at intercepting a projectile
                // than a bladed strike, and worse again against the fastest,
                // least visible of the three (the arquebus ball).
                //
                // RU-24 CALIBRATION PASS, PROVISIONAL: the launch values here
                // (900/700/300 and proportionally down the other three rows)
                // measured a pooled DefenceAttributableShare of 0.2220-0.2504
                // across seeds 1-20 at the default roster split -- below the
                // 0.25 floor for 19 of 20 seeds. Scaled x2.5 uniformly, which
                // preserves every within-row and cross-row ordering the
                // original hand-authored values established (Bangkaw >
                // Busog > Arquebus attacker, and each defender's relative
                // ranking against the other three) while lifting the pooled
                // average clear of the floor. Still a gameplay tuning
                // choice, not a historical measurement -- see class remarks.
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

                // Ranged defender vs any attacker — new. A ranged-armed
                // warrior has little to no close-quarters interception
                // skill, worst for the arquebus (no blade at all in this
                // preset's simplification, and both hands committed to the
                // weapon), decreasing in that order.
                //
                // RU-24 CALIBRATION PASS, PROVISIONAL: this is the single
                // largest drag on the pooled band (a) measurement -- melee
                // weapons fire far more often than ranged ones (4-8 tick
                // cooldowns versus 25-240), so most of a melee attacker's
                // huge attack volume that happens to land on the ranged
                // quarter of the roster was passing through this
                // under-25%-defended cell. Scaled x3 uniformly from launch,
                // preserving every ordering the original values established.
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

                // RU-45 PROVISIONAL: ShieldId.TallHardwood defender keys, the
                // roster's only two shielded entries. The four melee-attacker
                // cells for each are restated verbatim from
                // PhilippineCombatPresetV2 (V2:278-286), which authored these
                // exact defender/shield/attacker triples under its own
                // four-loadout roster. The three ranged-attacker cells are
                // new: each is that same weapon's ratio (~40% for Kalis,
                // ~30-36% for Itak, derived from the four reused V2 cells)
                // applied to this preset's own shieldless-row cell for the
                // same attacker above, rounded to the nearest 25 basis
                // points. Per-cell values are lower than the shieldless row
                // by design -- see the class remarks above on why the shield
                // channel, not this one, carries the rest of the difference.
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
            };

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>
        {
            // Restated verbatim from PhilippineCombatPresetV4.
            [(WeaponId.Kampilan, ShieldId.None)] = 1_000,
            [(WeaponId.Wasay, ShieldId.None)] = 900,
            [(WeaponId.Kalis, ShieldId.None)] = 1_350,
            [(WeaponId.Itak, ShieldId.None)] = 1_450,

            // New. A ranged-armed warrior's own footwork is unarmored by a
            // shield or a parrying blade, so its evasive step carries more of
            // its total defense than a melee weapon's does; heaviest for the
            // arquebus, whose bearer commits both hands and full attention to
            // the weapon and has the least to spare for anything else.
            [(WeaponId.Bangkaw, ShieldId.None)] = 1_600,
            [(WeaponId.Busog, ShieldId.None)] = 1_700,
            [(WeaponId.Arquebus, ShieldId.None)] = 1_800,

            // RU-45 PROVISIONAL: the two ShieldId.TallHardwood defender keys.
            // Restated verbatim from PhilippineCombatPresetV2 (V2:315-316),
            // which authored these same two defender/shield pairs.
            [(WeaponId.Kalis, ShieldId.TallHardwood)] = 1_000,
            [(WeaponId.Itak, ShieldId.TallHardwood)] = 1_100,
        };

        var hardShareBases = new Dictionary<WeaponId, int>
        {
            // Restated verbatim from PhilippineCombatPresetV4.
            [WeaponId.Kampilan] = 3_300,
            [WeaponId.Wasay] = 4_000,
            [WeaponId.Kalis] = 1_200,
            [WeaponId.Itak] = 1_800,

            // New. Keyed by attacking weapon: the share of an accepted
            // weapon-channel hit that arrests rather than brushes. A thrown
            // spear carries real mass behind it; an arrow does not; an
            // arquebus ball is a hard, punching hit that either lands
            // squarely or is deflected almost entirely.
            [WeaponId.Bangkaw] = 2_200,
            [WeaponId.Busog] = 900,
            [WeaponId.Arquebus] = 4_500,
        };

        var hardShareMultipliers = new Dictionary<WeaponId, int>
        {
            // Restated verbatim from PhilippineCombatPresetV4.
            [WeaponId.Kampilan] = 1_150,
            [WeaponId.Wasay] = 1_050,
            [WeaponId.Kalis] = 750,
            [WeaponId.Itak] = 700,

            // New. Keyed by defending weapon: a ranged-armed defender has
            // little blade skill to convert a weapon-channel hit into a
            // glancing share, decreasing further for the less blade-like
            // weapons.
            [WeaponId.Bangkaw] = 600,
            [WeaponId.Busog] = 400,
            [WeaponId.Arquebus] = 300,
        };

        return new ClashProfile(
            weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel,
            hardShareBases,
            hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// Authors a melee <see cref="WeaponProfile"/> from world units rather
    /// than raw fixed-point values, restated exactly from
    /// PhilippineCombatPresetV4's private helper of the same name and shape.
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
    /// world-units-per-tick rather than raw fixed-point values. Melee combo
    /// chaining is disabled outright (zero open chance, matching
    /// PhilippineCombatPresetV2's zero-chance convention for a deliberate
    /// no-op) since none of the three ranged weapons this preset declares
    /// forms a combo chain.
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
