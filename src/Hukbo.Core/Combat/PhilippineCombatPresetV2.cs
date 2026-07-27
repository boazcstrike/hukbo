using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 2 of the pre-colonial Philippine combat preset: version 1's
/// targeting weights, plus per-weapon damage, reach, and attack cooldown
/// split by grip, plus a six-entry roster that fields a solo and a paired
/// loadout for each one-handed weapon.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph. Each preset version
/// is a frozen snapshot: version 1's values are restated here rather than
/// referenced, so retuning version 2 can never reach back and move a hash
/// version 1's replays depend on.
/// <para>
/// Every attribute value below is a provisional gameplay tuning value, not a
/// historical measurement, and none of them may be cited back into
/// docs/research/HISTORICAL_1500s_WEAPONS.md. Weapon names carry evidence
/// tiers recorded on <see cref="WeaponId"/> and shown in the agent inspector.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV2
{
    public const int Version = 2;

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

        // PROVISIONAL gameplay tuning values, not historical measurements.
        // What justifies them is the physical character of the objects —
        // length, where the mass sits, how many hands the thing takes — not
        // any source on how hard a sixteenth-century blade hit.
        //
        // Two axes, not one. Across weapons the two-handed weapons top raw
        // throughput, which is deliberate headroom for the attack
        // combinations a later preset gives the one-handed weapons. Within a
        // one-handed weapon, dropping the shield buys one damage and one
        // world unit of reach and gives up the shield's halving of chest and
        // abdomen targeting weight — neither strictly dominates, which is the
        // requirement, because a choice that always resolves the same way is
        // not a choice.
        //
        // The paired Kalis row is exactly version 1's global defaults, which
        // makes it the control: if a battle's character shifts unexpectedly,
        // shielded Kalis warriors are the ones whose behaviour should not
        // have moved at all.
        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                Profile(damage: 15, reachWorldUnits: 16, cooldownTicks: 7)),

            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(
                Profile(damage: 18, reachWorldUnits: 13, cooldownTicks: 8)),

            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                Profile(damage: 11, reachWorldUnits: 13, cooldownTicks: 5),
                Profile(damage: 10, reachWorldUnits: 12, cooldownTicks: 5)),

            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                Profile(damage: 9, reachWorldUnits: 11, cooldownTicks: 4),
                Profile(damage: 8, reachWorldUnits: 10, cooldownTicks: 4)),
        };

        var armors = new[] { ArmorId.LightOrganic };

        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = TargetWeightProfiles.FromMultiplierOverrides(
                new Dictionary<BodyPart, int>()),

            // PROVISIONAL gameplay tuning, not a historical measurement:
            // a tall hardwood shield halves (500 of 1000 basis points)
            // chest and abdomen targeting weight, raising the relative
            // probability of arm, leg, head, neck, and face hits without
            // inventing bonuses for those parts.
            [ShieldId.TallHardwood] =
                TargetWeightProfiles.FromMultiplierOverrides(
                    new Dictionary<BodyPart, int>
                    {
                        [BodyPart.Chest] = 500,
                        [BodyPart.Abdomen] = 500,
                    }),
        };

        // Roster order is part of the content-hash contract and is indexed by
        // Scenario.RosterCounts. The ordering rule is stated rather than
        // incidental: weapon order first, then solo before paired within a
        // weapon. The two-handed weapons declare no paired entry because no
        // shield may be carried with them.
        var roster = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV2,
            Version,
            general,
            weaponTargets,
            armors,
            shieldMultipliers,
            roster,
            weaponAttributes);
    }

    /// <summary>
    /// Authors one profile in the units a person reasons in — world units of
    /// reach — and stores the raw fixed-point value the simulation reads.
    /// </summary>
    private static WeaponProfile Profile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks) =>
        new(damage, reachWorldUnits * FixedPoint.Scale, cooldownTicks);
}
