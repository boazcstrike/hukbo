using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Covers the preset V2 weapon attribute layer: the identity contract the
/// rename had to preserve, the resolver's two branches, the three
/// construction invariants, and the reach floor asserted per profile across
/// every registered preset rather than only against V2.
/// </summary>
public sealed class WeaponProfileTests
{
    [Theory]
    [InlineData(WeaponId.Kampilan, 1)]
    [InlineData(WeaponId.Wasay, 2)]
    [InlineData(WeaponId.Kalis, 3)]
    [InlineData(WeaponId.Itak, 4)]
    public void WeaponId_KeepsItsNumericValueAcrossTheRename(
        WeaponId weapon,
        int expected)
    {
        // The numeric value is the hashed quantity, so renaming the symbols
        // from Kampilan/Wasay/Kalis/Bolo had to leave every
        // one of these alone. A change here silently invalidates every replay
        // recorded against preset V1.
        Assert.Equal(expected, (int)weapon);
    }

    [Fact]
    public void EveryCombatPresetIdIsRegistered()
    {
        foreach (var id in Enum.GetValues<CombatPresetId>())
        {
            Assert.True(
                CombatPresetRegistry.IsRegistered(id),
                $"Combat preset {id} is declared but not registered.");
            Assert.NotNull(CombatPresetRegistry.Get(id));
        }
    }

    [Fact]
    public void PresetV1DeclaresNoWeaponProfilesAndV2Does()
    {
        // V1 predates the attribute layer and must keep taking damage, reach,
        // and cooldown from the scenario. That is also why it contributes
        // nothing to the content hash here, which is what keeps its pinned
        // hash where it was.
        Assert.False(PhilippineCombatPreset.Rules.HasWeaponProfiles);
        Assert.True(PhilippineCombatPresetV2.Rules.HasWeaponProfiles);
    }

    [Fact]
    public void PresetV2ContentHashDiffersFromV1()
    {
        Assert.NotEqual(
            PhilippineCombatPreset.Rules.ContentHash,
            PhilippineCombatPresetV2.Rules.ContentHash);
    }

    [Fact]
    public void PresetV2RosterDeclaresSoloBeforePairedWithinEachWeapon()
    {
        // Roster order is part of the content-hash contract and indexes
        // Scenario.RosterCounts, so it is pinned rather than described.
        var expected = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
        };

        Assert.Equal(expected, PhilippineCombatPresetV2.Rules.Roster);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan, WeaponGrip.TwoHanded)]
    [InlineData(WeaponId.Wasay, WeaponGrip.TwoHanded)]
    [InlineData(WeaponId.Kalis, WeaponGrip.OneHanded)]
    [InlineData(WeaponId.Itak, WeaponGrip.OneHanded)]
    public void PresetV2DeclaresTheApprovedGrip(
        WeaponId weapon,
        WeaponGrip expected) =>
        Assert.Equal(
            expected,
            PhilippineCombatPresetV2.Rules.ResolveWeaponGrip(weapon));

    [Fact]
    public void PresetV3RosterFieldsAllFourWeaponsSoloOnly()
    {
        // Roster order is part of the content-hash contract and indexes
        // Scenario.RosterCounts, so it is pinned rather than described. V3
        // omits the two paired loadouts V2 fields, per design section 2's
        // scope boundary.
        var expected = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
        };

        Assert.Equal(expected, PhilippineCombatPresetV3.Rules.Roster);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan, WeaponGrip.TwoHanded)]
    [InlineData(WeaponId.Wasay, WeaponGrip.TwoHanded)]
    [InlineData(WeaponId.Kalis, WeaponGrip.OneHanded)]
    [InlineData(WeaponId.Itak, WeaponGrip.OneHanded)]
    public void PresetV3DeclaresTheApprovedGrip(
        WeaponId weapon,
        WeaponGrip expected) =>
        Assert.Equal(
            expected,
            PhilippineCombatPresetV3.Rules.ResolveWeaponGrip(weapon));

    [Theory]
    // Weapon, shield, damage, reach in world units, cooldown ticks, combo
    // open chance, combo continue chance, combo max steps, combo cooldown
    // ticks. These are the six rows of the design's attribute table; the
    // paired Kalis row is exactly V1's global defaults and is the control.
    // Every combo open chance is 0 — V2's combo values are a true no-op, per
    // the combat preset V3 combinations plan section
    // 5 — and the other three combo columns borrow that weapon's V3 solo-row
    // combo values, which is what makes them positive without inventing a
    // divergent number.
    [InlineData(WeaponId.Kampilan, ShieldId.None, 15, 16, 7, 0, 3_000, 2, 4)]
    [InlineData(WeaponId.Wasay, ShieldId.None, 18, 13, 8, 0, 2_000, 2, 5)]
    [InlineData(WeaponId.Kalis, ShieldId.None, 11, 13, 5, 0, 4_500, 4, 3)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, 10, 12, 5, 0, 4_500, 4, 3)]
    [InlineData(WeaponId.Itak, ShieldId.None, 9, 11, 4, 0, 5_500, 5, 2)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, 8, 10, 4, 0, 5_500, 5, 2)]
    public void ResolveWeaponProfile_ReturnsTheAuthoredRow(
        WeaponId weapon,
        ShieldId shield,
        int expectedDamage,
        int expectedReachWorldUnits,
        int expectedCooldown,
        int expectedComboOpenChanceBasisPoints,
        int expectedComboContinueChanceBasisPoints,
        int expectedComboMaxSteps,
        int expectedComboCooldownTicks)
    {
        var profile = PhilippineCombatPresetV2.Rules.ResolveWeaponProfile(
            weapon,
            shield);

        Assert.Equal(expectedDamage, profile.DamagePerAttack);
        Assert.Equal(
            expectedReachWorldUnits * FixedPoint.Scale,
            profile.AttackRangeRaw);
        Assert.Equal(expectedCooldown, profile.AttackCooldownTicks);
        Assert.Equal(
            expectedComboOpenChanceBasisPoints,
            profile.ComboOpenChanceBasisPoints);
        Assert.Equal(
            expectedComboContinueChanceBasisPoints,
            profile.ComboContinueChanceBasisPoints);
        Assert.Equal(expectedComboMaxSteps, profile.ComboMaxSteps);
        Assert.Equal(
            expectedComboCooldownTicks,
            profile.ComboCooldownTicks);
    }

    [Theory]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void DroppingTheShieldBuysDamageAndReachAndCostsNoCadence(
        WeaponId weapon)
    {
        var rules = PhilippineCombatPresetV2.Rules;
        var solo = rules.ResolveWeaponProfile(weapon, ShieldId.None);
        var paired = rules.ResolveWeaponProfile(
            weapon,
            ShieldId.TallHardwood);

        // The trade is uniform across both one-handed weapons on purpose, so
        // it is explicable to a spectator and retunable in one place.
        Assert.Equal(paired.DamagePerAttack + 1, solo.DamagePerAttack);
        Assert.Equal(
            paired.AttackRangeRaw + FixedPoint.Scale,
            solo.AttackRangeRaw);
        Assert.Equal(paired.AttackCooldownTicks, solo.AttackCooldownTicks);
    }

    [Fact]
    public void EveryProfileOfEveryRegisteredPresetClearsTheReachFloor()
    {
        // Asserted per profile and over the whole registry, not just V2: a
        // one-handed weapon's paired reach is always the shorter of the two,
        // so a future retune that shaves a world unit off a paired row is the
        // most likely way anyone ever trips this floor.
        foreach (var id in Enum.GetValues<CombatPresetId>())
        {
            var rules = CombatPresetRegistry.Get(id);
            if (!rules.HasWeaponProfiles)
            {
                continue;
            }

            foreach (var loadout in rules.Roster)
            {
                var profile = rules.ResolveWeaponProfile(
                    loadout.Weapon,
                    loadout.Shield);

                Assert.True(
                    profile.AttackRangeRaw >
                        CombatRuleset.MinimumProfileReachRawExclusive,
                    $"Preset {id}: {loadout.Weapon} with {loadout.Shield} " +
                    $"has reach {profile.AttackRangeRaw} raw, at or below " +
                    $"the floor of " +
                    $"{CombatRuleset.MinimumProfileReachRawExclusive}.");
            }
        }
    }

    [Fact]
    public void EnlargedCollisionBodyRaisesTheReachFloorButTheItakPairedProfileStillClearsItByOneAndAHalfWorldUnits()
    {
        // Task C1
        // (the collision report and window shell plan)
        // moved CollisionRules.DefaultBodyRadiusRaw from four world units to
        // 4.25, which raises MinimumProfileReachRawExclusive (twice the body
        // radius) from eight world units to 8.5. Design section 1.2 rejected
        // a 5.0-unit radius specifically because it would have pushed the
        // floor to ten world units and rejected the Itak's shield-paired
        // profile outright, and the deadlock finding recorded against
        // LastStandFormationTests then ruled out 4.5 as well. This test pins
        // both sides of that arithmetic so a future retune of either the
        // radius or the Itak's paired reach fails here instead of silently
        // eroding the margin.
        Assert.Equal(
            (17 * FixedPoint.Scale) / 2,
            CombatRuleset.MinimumProfileReachRawExclusive);

        var v3 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV3);
        var itakPaired = v3.ResolveWeaponProfile(WeaponId.Itak, ShieldId.TallHardwood);

        Assert.Equal(10 * FixedPoint.Scale, itakPaired.AttackRangeRaw);
        Assert.Equal(
            (3 * FixedPoint.Scale) / 2,
            itakPaired.AttackRangeRaw - CombatRuleset.MinimumProfileReachRawExclusive);
    }

    [Fact]
    public void TwoHandedWeaponPairedWithAShieldInTheRosterThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() => BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                    Profile(15, 16, 7)),
            },
            roster:
            [
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.TallHardwood),
            ]));

        Assert.Contains("two-handed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneHandedWeaponMissingItsPairedProfileThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() => BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = new(
                    WeaponGrip.OneHanded,
                    Profile(15, 16, 7),
                    Paired: null),
            }));

        Assert.Contains(
            "no paired profile",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TwoHandedWeaponDeclaringAPairedProfileThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() => BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = new(
                    WeaponGrip.TwoHanded,
                    Profile(15, 16, 7),
                    Profile(14, 15, 7)),
            }));

        Assert.Contains(
            "declares a paired profile",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileAtTheReachFloorThrowsAtConstruction()
    {
        // Eight world units is exactly two body radii: a warrior carrying
        // this would advance into body contact and then never be able to
        // strike.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildRuleset(
                attributes: new Dictionary<WeaponId, WeaponAttributes>
                {
                    [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                        Profile(15, reachWorldUnits: 8, cooldownTicks: 7)),
                }));

        Assert.Contains("never be able to strike", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PresetDeclaringAttributesForOnlySomeWeaponsThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() => BuildRuleset(
            weapons: [WeaponId.Kampilan, WeaponId.Wasay],
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                    Profile(15, 16, 7)),
            }));

        Assert.Contains(
            "for every weapon or for none",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvingAProfileFromAPresetWithoutThemThrows() =>
        Assert.Throws<InvalidOperationException>(() =>
            PhilippineCombatPreset.Rules.ResolveWeaponProfile(
                WeaponId.Kampilan,
                ShieldId.None));

    [Fact]
    public void PresetV2ReportsItsLargestProfileDamage() =>
        // The Wasay's 18, which is what Scenario.Validate has to guard its
        // same-tick damage accumulator against rather than the scenario's
        // own global value of 10.
        Assert.Equal(18, PhilippineCombatPresetV2.Rules.MaximumProfileDamagePerAttack);

    [Fact]
    public void EveryProfileOfEveryRegisteredPresetDeclaresRangedFieldsConsistentWithItsKind()
    {
        // Every registered preset's every profile must land on one of the two
        // halves CombatRuleset (via WeaponProfile.ValidateRangedFields)
        // enforces at construction: melee declares all three ranged fields
        // zero, ranged declares all three non-zero. This used to assert only
        // the melee half, which was true only because RU-12's
        // PrecolonialPhilippinesV5 — the first registered preset to field a
        // ranged row — was not registered yet. It is registered now, so this
        // sweeps both halves rather than hard-coding which presets are melee.
        //
        // Deriving "is this profile ranged" from the same three fields the
        // assertion then checks would be tautological: WeaponProfile
        // .ValidateRangedFields already forces every profile that manages to
        // construct into all-zero-or-all-non-zero, so the self-referential
        // predicate could never disagree with the fields it reads and this
        // fact could never fail. The kind has to come from a signal
        // independent of the three fields under test, so it comes from the
        // weapon's identity instead, via RangedPhaseProjection.Derive — the
        // production switch over WeaponId (Bangkaw, Busog, Arquebus) that
        // decides whether a warrior's readable draw cycle exists at all. A
        // melee weapon wrongly given non-zero ranged fields, or a ranged
        // weapon wrongly given all-zero ones, disagrees with this second,
        // independent source and fails here even though it would have
        // constructed cleanly.
        foreach (var id in Enum.GetValues<CombatPresetId>())
        {
            if (!CombatPresetRegistry.IsRegistered(id))
            {
                continue;
            }

            var rules = CombatPresetRegistry.Get(id);
            if (!rules.HasWeaponProfiles)
            {
                continue;
            }

            foreach (var loadout in rules.Roster)
            {
                var profile = rules.ResolveWeaponProfile(
                    loadout.Weapon,
                    loadout.Shield);

                AssertRangedFieldsAgreeWithWeaponIdentity(loadout.Weapon, profile);
            }
        }
    }

    /// <summary>
    /// Asserts a profile's declared ranged fields agree with the independent
    /// signal <see cref="RangedPhaseProjection.Derive"/> reads from the
    /// weapon's identity alone: a weapon the projection recognizes as ranged
    /// must declare all three fields non-zero, and a weapon it does not
    /// recognize must declare all three zero. Split out from
    /// <see cref="EveryProfileOfEveryRegisteredPresetDeclaresRangedFieldsConsistentWithItsKind"/>
    /// so a hand-built weapon/profile disagreement can drive this exact
    /// mechanism directly, rather than only through whatever the registry
    /// currently happens to field.
    /// </summary>
    private static void AssertRangedFieldsAgreeWithWeaponIdentity(
        WeaponId weapon,
        WeaponProfile profile)
    {
        var declaresRangedFields = profile.ProjectileSpeedRaw != 0 ||
            profile.StandoffDistanceRaw != 0 ||
            profile.FlightTickCeiling != 0;

        var (phase, _) = RangedPhaseProjection.Derive(
            weapon,
            attackCooldownRemaining: 0,
            attackCooldownTicks: profile.AttackCooldownTicks);
        var isRangedWeaponIdentity = phase != RangedPhase.None;

        Assert.Equal(isRangedWeaponIdentity, declaresRangedFields);

        if (declaresRangedFields)
        {
            Assert.NotEqual(0, profile.ProjectileSpeedRaw);
            Assert.NotEqual(0, profile.StandoffDistanceRaw);
            Assert.NotEqual(0, profile.FlightTickCeiling);
        }
        else
        {
            Assert.Equal(0, profile.ProjectileSpeedRaw);
            Assert.Equal(0, profile.StandoffDistanceRaw);
            Assert.Equal(0, profile.FlightTickCeiling);
        }
    }

    [Theory]
    // Exactly one of the three ranged fields declared, the other two left at
    // the melee default of zero. Any one non-zero field is enough to mark the
    // profile as an attempted ranged declaration, so each case must throw.
    [InlineData(0, 5, 3)]
    [InlineData(4, 0, 3)]
    [InlineData(4, 5, 0)]
    public void RangedProfileDeclaringOnlySomeOfTheThreeFieldsThrows(
        int projectileSpeedWorldUnits,
        int standoffWorldUnits,
        int flightTickCeiling)
    {
        var exception = Assert.Throws<ArgumentException>(() => BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                    RangedProfile(
                        15,
                        reachWorldUnits: 16,
                        cooldownTicks: 7,
                        projectileSpeedWorldUnits,
                        standoffWorldUnits,
                        flightTickCeiling)),
            }));

        Assert.Contains(
            "declares a ranged weapon",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandoffDistanceAtTheProfilesOwnReachThrows()
    {
        // Sixteen world units of reach, sixteen of standoff: exactly at the
        // boundary, which the row deliberately excludes rather than allows.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildRuleset(
                attributes: new Dictionary<WeaponId, WeaponAttributes>
                {
                    [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                        RangedProfile(
                            15,
                            reachWorldUnits: 16,
                            cooldownTicks: 7,
                            projectileSpeedWorldUnits: 4,
                            standoffWorldUnits: 16,
                            flightTickCeiling: 30)),
                }));

        Assert.Contains(
            "one collision nudge",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandoffDistanceBeyondTheProfilesOwnReachThrows()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildRuleset(
                attributes: new Dictionary<WeaponId, WeaponAttributes>
                {
                    [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                        RangedProfile(
                            15,
                            reachWorldUnits: 16,
                            cooldownTicks: 7,
                            projectileSpeedWorldUnits: 4,
                            standoffWorldUnits: 20,
                            flightTickCeiling: 30)),
                }));

        Assert.Contains(
            "beyond its own reach",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RangedProfileWithAllThreeDeclaredAndStandoffStrictlyInsideReachConstructs()
    {
        var rules = BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                    RangedProfile(
                        15,
                        reachWorldUnits: 16,
                        cooldownTicks: 7,
                        projectileSpeedWorldUnits: 4,
                        standoffWorldUnits: 12,
                        flightTickCeiling: 30)),
            });

        var profile = rules.ResolveWeaponProfile(WeaponId.Kampilan, ShieldId.None);
        Assert.Equal(4 * FixedPoint.Scale, profile.ProjectileSpeedRaw);
        Assert.Equal(12 * FixedPoint.Scale, profile.StandoffDistanceRaw);
        Assert.Equal(30, profile.FlightTickCeiling);
    }

    [Fact]
    public void MeleeProfileWithAllThreeRangedFieldsZeroConstructs()
    {
        // Every existing preset weapon is melee and relies on this: the
        // default zero for all three ranged fields must remain a valid,
        // no-op declaration so presets V1 through V4 keep constructing
        // untouched.
        var rules = BuildRuleset(
            attributes: new Dictionary<WeaponId, WeaponAttributes>
            {
                [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                    Profile(15, 16, 7)),
            });

        var profile = rules.ResolveWeaponProfile(WeaponId.Kampilan, ShieldId.None);
        Assert.Equal(0, profile.ProjectileSpeedRaw);
        Assert.Equal(0, profile.StandoffDistanceRaw);
        Assert.Equal(0, profile.FlightTickCeiling);
    }

    private static WeaponProfile RangedProfile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks,
        int projectileSpeedWorldUnits,
        int standoffWorldUnits,
        int flightTickCeiling) =>
        new(
            damage,
            reachWorldUnits * FixedPoint.Scale,
            cooldownTicks,
            ProjectileSpeedRaw: projectileSpeedWorldUnits * FixedPoint.Scale,
            StandoffDistanceRaw: standoffWorldUnits * FixedPoint.Scale,
            FlightTickCeiling: flightTickCeiling);

    private static WeaponProfile Profile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks) =>
        new(damage, reachWorldUnits * FixedPoint.Scale, cooldownTicks);

    /// <summary>
    /// A minimal ruleset whose only interesting content is the attribute
    /// table under test, so an invariant failure is unambiguous.
    /// </summary>
    private static CombatRuleset BuildRuleset(
        IReadOnlyDictionary<WeaponId, WeaponAttributes> attributes,
        IReadOnlyList<WeaponId>? weapons = null,
        IReadOnlyList<CombatLoadout>? roster = null)
    {
        var general = new TargetWeightProfile(
            BodyPartCatalog.Ordered.Select(part => (part, 1)).ToArray());
        var weaponTargets = (weapons ?? [WeaponId.Kampilan])
            .ToDictionary(weapon => weapon, _ => general);
        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = general,
            [ShieldId.TallHardwood] = general,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV2,
            PhilippineCombatPresetV2.Version,
            general,
            weaponTargets,
            [ArmorId.LightOrganic],
            shieldMultipliers,
            roster ??
            [
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.None),
            ],
            attributes);
    }
}
