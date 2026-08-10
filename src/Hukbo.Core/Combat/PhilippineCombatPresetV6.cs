using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 6 of the pre-colonial Philippine combat preset: version 4's tables,
/// roster, ranks, and clash profile restated without modification, with the
/// melee attack cooldown, combo cooldown, and damage of all six loadouts
/// retuned so that blows land roughly half as often and hurt roughly twice as
/// much.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph, matching the
/// convention every earlier preset version follows. Version 6 is a frozen
/// snapshot in the same sense versions 1 through 5 are: version 4's values are
/// restated here rather than referenced, so retuning version 6 can never reach
/// back and move a hash version 4's replays depend on. Versions 1 through 5
/// stay registered and unmodified.
/// <para>
/// Why this version exists. An interactive smoke run on 2026-08-11 failed three
/// weapon-clash legibility rows — CL-1, CL-3, and CL-7 in
/// docs/development/smoke-checklist.md. Every effect rendered; the observer
/// simply could not attribute an individual clash cross or event-feed line to
/// the individual blow that produced it. Under version 4 an Itak warrior
/// commits a blow every four ticks, which is 200 milliseconds at the 20 Hz tick
/// rate, while the client's presentation timeline for that same blow already
/// runs 170 milliseconds. Consecutive swings therefore very nearly abut, and
/// the artefact rate is the attack rate. Halving the attack rate halves both
/// the event-feed line rate and the clash-cross rate.
/// </para>
/// <para>
/// The governing constraint is that damage per tick is held as close to version
/// 4's as integer arithmetic allows — every loadout is within two per cent. Time
/// to kill is therefore unchanged, so battle duration and termination behaviour
/// should move very little, while blows per kill roughly halves. That invariant
/// is not merely commentary: <c>CombatCadenceV6Tests</c> asserts it in integer
/// arithmetic, so a later edit that moves a damage value without moving its
/// cooldown fails the build.
/// </para>
/// <para>
/// Every attribute value below is a PROVISIONAL gameplay tuning value, not a
/// historical measurement, and none of them may be cited back into
/// docs/research/HISTORICAL_1500s_WEAPONS.md. Rounding at the margin — 26 rather
/// than 25 for the Kampilan, five rather than four ticks for the Itak combo
/// cooldown — is a judgement call recorded in sections 3.2 and 3.3 of
/// docs/plans/2026-08-11-combat-cadence-v6-design.md. Weapon names carry
/// evidence tiers recorded on <see cref="WeaponId"/> and shown in the agent
/// inspector.
/// </para>
/// <para>
/// The weapon-to-rank assignment below — Datu with the Kampilan, Maharlika with
/// the Wasay, Timawa with the Kalis, Aliping Namamahay with the Itak — is
/// carried over from version 4 unchanged. It is a provisional gameplay tuning
/// choice, not a historical claim. No sixteenth-century source assigns a
/// particular weapon to a particular social class, and
/// docs/research/HISTORICAL_1500s_RANKS.md is explicit that rank carries no
/// combat-strength value of its own. The per-rank fighter levels are the same
/// caveat.
/// </para>
/// <para>
/// Kalis and Itak are <see cref="WeaponGrip.OneHanded"/>, and
/// <see cref="CombatRuleset"/> requires a one-handed weapon to declare a paired
/// profile even when no roster entry ever resolves it. This preset's roster
/// fields only the solo loadout for each, so the paired rows below are
/// unreachable through <see cref="CombatRuleset.Roster"/>; they exist solely to
/// satisfy that construction invariant, exactly as in version 4. They are
/// retuned alongside the solo rows so that the two stay a consistent pair.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV6
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

        // Target weights, per weapon, are restated exactly from
        // PhilippineCombatPresetV4. This version changes cadence and damage
        // only; where a blow lands is untouched.
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
        };

        // The only table this version changes. Reach and the three combo
        // *chance* fields are restated exactly from PhilippineCombatPresetV4;
        // damage, cooldownTicks, and comboCooldownTicks are retuned.
        //
        // Damage per tick, version 4 to version 6:
        //   Kampilan        15/7  = 2.143  ->  26/12 = 2.167  (+1.1%)
        //   Wasay           18/8  = 2.250  ->  32/14 = 2.286  (+1.6%)
        //   Kalis solo      11/5  = 2.200  ->  22/10 = 2.200  ( 0.0%)
        //   Kalis paired    10/5  = 2.000  ->  20/10 = 2.000  ( 0.0%)
        //   Itak solo        9/4  = 2.250  ->  20/9  = 2.222  (-1.2%)
        //   Itak paired      8/4  = 2.000  ->  18/9  = 2.000  ( 0.0%)
        //
        // The one deliberate outlier is the Itak combo cooldown. Two ticks
        // across up to five steps delivered five blows in eight ticks, which is
        // the single least decomposable burst in the battle; stretching it to
        // five ticks costs the chain eleven per cent of its damage per tick and
        // that cost is the point, not a side effect.
        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 26,
                    reachWorldUnits: 16,
                    cooldownTicks: 12,
                    comboOpenChanceBasisPoints: 2_000,
                    comboContinueChanceBasisPoints: 3_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 7)),

            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 32,
                    reachWorldUnits: 13,
                    cooldownTicks: 14,
                    comboOpenChanceBasisPoints: 1_000,
                    comboContinueChanceBasisPoints: 2_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 9)),

            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 22,
                    reachWorldUnits: 13,
                    cooldownTicks: 10,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 6),
                Profile(
                    damage: 20,
                    reachWorldUnits: 12,
                    cooldownTicks: 10,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 6)),

            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 20,
                    reachWorldUnits: 11,
                    cooldownTicks: 9,
                    comboOpenChanceBasisPoints: 4_500,
                    comboContinueChanceBasisPoints: 5_500,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 5),
                Profile(
                    damage: 18,
                    reachWorldUnits: 10,
                    cooldownTicks: 9,
                    comboOpenChanceBasisPoints: 4_500,
                    comboContinueChanceBasisPoints: 5_500,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 5)),
        };

        var armors = new[] { ArmorId.LightOrganic };

        // Restated exactly from PhilippineCombatPresetV4. This version's roster
        // never pairs a shield with any weapon, so only the ShieldId.None row
        // is ever resolved; ShieldId.TallHardwood is carried across unchanged
        // so this data stays a faithful copy rather than a partial one.
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

        // Roster order is part of the content-hash contract and is indexed by
        // Scenario.RosterCounts. Restated exactly from
        // PhilippineCombatPresetV4, including the rank assignment — see the
        // class remarks for the caveat on it.
        var roster = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None, RankId.Maharlika),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, RankId.AlipingNamamahay),
        };

        // Per-rank fighter levels, restated exactly from
        // PhilippineCombatPresetV4. Provisional gameplay tuning with no
        // evidentiary standing — see the class remarks. Ayuey is declared even
        // though no roster entry fields it, because this is the game's one
        // canonical per-rank level table, not a table scoped to this roster.
        var rankLevels = new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Maharlika] = 2,
            [RankId.Timawa] = 2,
            [RankId.AlipingNamamahay] = 1,
            [RankId.Ayuey] = 1,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV6,
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
    /// The defensive-interception tuning data, restated exactly from
    /// <see cref="PhilippineCombatPresetV4"/>. Not one cell below is retuned:
    /// this version changes how often a blow is thrown and how hard it lands,
    /// never how likely it is to be intercepted. PROVISIONAL gameplay tuning
    /// throughout; see docs/research/WEAPON_CLASH_1500s.md and CLAUDE.md
    /// section 7.
    /// </summary>
    private static ClashProfile BuildClashProfile()
    {
        var weaponIntercept = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>
        {
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
        };

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>
        {
            [(WeaponId.Kampilan, ShieldId.None)] = 1_000,
            [(WeaponId.Wasay, ShieldId.None)] = 900,
            [(WeaponId.Kalis, ShieldId.None)] = 1_350,
            [(WeaponId.Itak, ShieldId.None)] = 1_450,
        };

        var hardShareBases = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 3_300,
            [WeaponId.Wasay] = 4_000,
            [WeaponId.Kalis] = 1_200,
            [WeaponId.Itak] = 1_800,
        };

        var hardShareMultipliers = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 1_150,
            [WeaponId.Wasay] = 1_050,
            [WeaponId.Kalis] = 750,
            [WeaponId.Itak] = 700,
        };

        return new ClashProfile(
            weaponIntercept: weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: hardShareBases,
            hardShareMultipliers: hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// Authors one profile in the units a person reasons in — world units of
    /// reach — and stores the raw fixed-point value the simulation reads.
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
}
