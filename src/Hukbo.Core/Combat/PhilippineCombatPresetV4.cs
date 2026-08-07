using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 4 of the pre-colonial Philippine combat preset: version 3's four
/// solo loadouts, each now assigned a <see cref="RankId"/>, plus a per-rank
/// fighter level table that replaces the placeholder level version 3 uses
/// uniformly for every warrior.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph, matching the
/// convention every earlier preset version follows. Version 4 is a frozen
/// snapshot in the same sense versions 1 through 3 are: version 3's values
/// are restated here rather than referenced, so retuning version 4 can never
/// reach back and move a hash version 3's replays depend on.
/// <para>
/// Every attribute value below is a provisional gameplay tuning value, not a
/// historical measurement, and none of them may be cited back into
/// docs/research/HISTORICAL_1500s_WEAPONS.md. Weapon names carry evidence
/// tiers recorded on <see cref="WeaponId"/> and shown in the agent inspector.
/// </para>
/// <para>
/// The weapon-to-rank assignment below — Datu with the Kampilan, Maharlika
/// with the Wasay, Timawa with the Kalis, Aliping Namamahay with the Itak —
/// is a provisional gameplay tuning choice, not a historical claim. No
/// sixteenth-century source assigns a particular weapon to a particular
/// social class, and docs/research/HISTORICAL_1500s_RANKS.md is explicit
/// that rank carries no combat-strength value of its own. The per-rank
/// fighter levels declared below are the same caveat: provisional gameplay
/// tuning with no evidentiary standing whatsoever, and the inspector must
/// not present them as historical.
/// </para>
/// <para>
/// Kalis and Itak are <see cref="WeaponGrip.OneHanded"/>, and
/// <see cref="CombatRuleset"/> requires a one-handed weapon to declare a
/// paired profile even when no roster entry ever resolves it — a missing
/// paired profile is an error, not a silent fallback to the solo one. This
/// preset's roster fields only the solo loadout for each, so the paired rows
/// below are unreachable through <see cref="CombatRuleset.Roster"/>; they
/// exist solely to satisfy that construction invariant. Each carries its
/// weapon's version 3 paired damage/reach/cooldown values (the same numbers
/// version 3 restated from version 2 for its own paired row) and, for the
/// four combo fields, the same values as that weapon's solo row here — the
/// neutral-default convention version 3 already uses, applied consistently
/// to an unrostered row.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV4
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
        // PhilippineCombatPresetV3 for the four weapons V4 fields.
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

        // Damage/reach/cooldown and the four combo fields are restated
        // exactly from PhilippineCombatPresetV3 for these four weapons (both
        // grip and, for the two one-handed weapons, both the solo and
        // paired rows).
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
                    comboOpenChanceBasisPoints: 4_500,
                    comboContinueChanceBasisPoints: 5_500,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2),
                Profile(
                    damage: 8,
                    reachWorldUnits: 10,
                    cooldownTicks: 4,
                    comboOpenChanceBasisPoints: 4_500,
                    comboContinueChanceBasisPoints: 5_500,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2)),
        };

        var armors = new[] { ArmorId.LightOrganic };

        // Restated exactly from PhilippineCombatPresetV3. V4's roster never
        // pairs a shield with any weapon, so only the ShieldId.None row is
        // ever resolved; ShieldId.TallHardwood is carried across unchanged
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

        // Roster order is part of the content-hash contract and is indexed
        // by Scenario.RosterCounts. All four entries are solo, matching V3's
        // roster shape, but each now carries the RankId that is this
        // version's only newly authored roster data — see the class
        // remarks for the caveat on this assignment.
        var roster = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None, RankId.Maharlika),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, RankId.Timawa),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, RankId.AlipingNamamahay),
        };

        // Per-rank fighter levels: the other newly authored data this
        // version adds. Provisional gameplay tuning with no evidentiary
        // standing — see the class remarks. Ayuey is declared here even
        // though no roster entry fields it, on the same principle preset V3
        // already uses for its unreachable paired weapon profiles: this is
        // the game's one canonical per-rank level table, not a table scoped
        // to this roster alone.
        var rankLevels = new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Maharlika] = 2,
            [RankId.Timawa] = 2,
            [RankId.AlipingNamamahay] = 1,
            [RankId.Ayuey] = 1,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV4,
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
    /// The defensive-interception tuning data for V4's four-loadout,
    /// all-solo roster. Every cell below is restated exactly from
    /// <see cref="PhilippineCombatPresetV3"/>'s shieldless (<see
    /// cref="ShieldId.None"/>) rows, which are the only rows V4's roster ever
    /// resolves — see <see cref="CombatRuleset"/>'s
    /// <c>ValidateClashProfileCoversTheRoster</c>, which requires coverage
    /// only for the (defender weapon, defender shield, attacker weapon)
    /// combinations the roster actually contains. PROVISIONAL gameplay
    /// tuning throughout; see docs/research/WEAPON_CLASH_1500s.md and
    /// CLAUDE.md section 7.
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
