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

    [Theory]
    // Weapon, shield, damage, reach in world units, cooldown ticks. These are
    // the six rows of the design's attribute table; the paired Kalis row is
    // exactly V1's global defaults and is the control.
    [InlineData(WeaponId.Kampilan, ShieldId.None, 15, 16, 7)]
    [InlineData(WeaponId.Wasay, ShieldId.None, 18, 13, 8)]
    [InlineData(WeaponId.Kalis, ShieldId.None, 11, 13, 5)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, 10, 12, 5)]
    [InlineData(WeaponId.Itak, ShieldId.None, 9, 11, 4)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, 8, 10, 4)]
    public void ResolveWeaponProfile_ReturnsTheAuthoredRow(
        WeaponId weapon,
        ShieldId shield,
        int expectedDamage,
        int expectedReachWorldUnits,
        int expectedCooldown)
    {
        var profile = PhilippineCombatPresetV2.Rules.ResolveWeaponProfile(
            weapon,
            shield);

        Assert.Equal(expectedDamage, profile.DamagePerAttack);
        Assert.Equal(
            expectedReachWorldUnits * FixedPoint.Scale,
            profile.AttackRangeRaw);
        Assert.Equal(expectedCooldown, profile.AttackCooldownTicks);
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
