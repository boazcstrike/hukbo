using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

public sealed class CombatConfigurationTests
{
    [Fact]
    public void BodyPartCatalog_OrderedMatchesEveryDeclaredBodyPartEnumMemberInAscendingOrder()
    {
        // Pins BodyPartCatalog.Ordered to the BodyPart enum itself: an added,
        // removed, or reordered enum member that is not mirrored in the
        // catalog would otherwise be silently unreachable by targeting and
        // absent from CombatRuleset.ContentHash.
        var expected = Enum.GetValues<BodyPart>().OrderBy(part => (int)part).ToArray();

        Assert.Equal(expected, BodyPartCatalog.Ordered);
    }

    [Fact]
    public void AttackResolution_PinsItsNumericValues()
    {
        // The resolution rides on every attack event and is folded into the
        // headless event hash, so a renumbering or a reordering silently moves
        // that hash for every seed. Pinning all five values and the declared
        // order makes such a change fail here rather than in a golden.
        Assert.Equal(0, (int)AttackResolution.Landed);
        Assert.Equal(1, (int)AttackResolution.ShieldBlocked);
        Assert.Equal(2, (int)AttackResolution.Parried);
        Assert.Equal(3, (int)AttackResolution.Deflected);
        Assert.Equal(4, (int)AttackResolution.Evaded);

        Assert.Equal(
            [
                AttackResolution.Landed,
                AttackResolution.ShieldBlocked,
                AttackResolution.Parried,
                AttackResolution.Deflected,
                AttackResolution.Evaded,
            ],
            Enum.GetValues<AttackResolution>());
    }

    [Fact]
    public void PhilippinePreset_UsesApprovedGeneralWeights()
    {
        var rules = PhilippineCombatPreset.Rules;

        Assert.Equal(10, rules.GeneralTargets.Get(BodyPart.WeaponArm));
        Assert.Equal(8, rules.GeneralTargets.Get(BodyPart.ShieldArm));
        Assert.Equal(9, rules.GeneralTargets.Get(BodyPart.Shoulder));
        Assert.Equal(9, rules.GeneralTargets.Get(BodyPart.Head));
        Assert.Equal(9, rules.GeneralTargets.Get(BodyPart.Neck));
        Assert.Equal(8, rules.GeneralTargets.Get(BodyPart.Face));
        Assert.Equal(7, rules.GeneralTargets.Get(BodyPart.Chest));
        Assert.Equal(7, rules.GeneralTargets.Get(BodyPart.Abdomen));
        Assert.Equal(8, rules.GeneralTargets.Get(BodyPart.Thigh));
        Assert.Equal(7, rules.GeneralTargets.Get(BodyPart.Knee));
        Assert.Equal(7, rules.GeneralTargets.Get(BodyPart.Shin));
        Assert.Equal(8, rules.GeneralTargets.Get(BodyPart.Hands));
        Assert.Equal(2, rules.GeneralTargets.Get(BodyPart.Feet));
    }

    [Theory]
    [InlineData(WeaponId.GreatBlade, BodyPart.Head, 10)]
    [InlineData(WeaponId.GreatBlade, BodyPart.Neck, 10)]
    [InlineData(WeaponId.GreatBlade, BodyPart.Shoulder, 9)]
    [InlineData(WeaponId.GreatBlade, BodyPart.WeaponArm, 8)]
    [InlineData(WeaponId.GreatBlade, BodyPart.ShieldArm, 8)]
    [InlineData(WeaponId.GreatBlade, BodyPart.Chest, 8)]
    [InlineData(WeaponId.GreatBlade, BodyPart.Feet, 2)]
    [InlineData(WeaponId.HeavyChopper, BodyPart.Shoulder, 10)]
    [InlineData(WeaponId.HeavyChopper, BodyPart.Head, 9)]
    [InlineData(WeaponId.HeavyChopper, BodyPart.WeaponArm, 9)]
    [InlineData(WeaponId.HeavyChopper, BodyPart.ShieldArm, 9)]
    [InlineData(WeaponId.HeavyChopper, BodyPart.Neck, 9)]
    [InlineData(WeaponId.ThrustingBlade, BodyPart.Abdomen, 10)]
    [InlineData(WeaponId.ThrustingBlade, BodyPart.Chest, 9)]
    [InlineData(WeaponId.ThrustingBlade, BodyPart.Neck, 8)]
    [InlineData(WeaponId.ThrustingBlade, BodyPart.WeaponArm, 10)]
    [InlineData(WeaponId.Bolo, BodyPart.WeaponArm, 10)]
    [InlineData(WeaponId.Bolo, BodyPart.ShieldArm, 10)]
    [InlineData(WeaponId.Bolo, BodyPart.Hands, 9)]
    [InlineData(WeaponId.Bolo, BodyPart.Neck, 8)]
    [InlineData(WeaponId.Bolo, BodyPart.Face, 8)]
    [InlineData(WeaponId.Bolo, BodyPart.Shoulder, 9)]
    public void PhilippinePreset_UsesApprovedWeaponOverrides(
        WeaponId weapon,
        BodyPart bodyPart,
        int expected)
    {
        Assert.Equal(
            expected,
            PhilippineCombatPreset.Rules.ResolveWeaponWeight(weapon, bodyPart));
    }

    [Fact]
    public void NoShieldAppliesTheDefaultMultiplierToEveryBodyPart()
    {
        var rules = PhilippineCombatPreset.Rules;

        foreach (var part in Enum.GetValues<BodyPart>())
        {
            Assert.Equal(1_000, rules.ResolveDefenseMultiplier(ShieldId.None, part));
        }
    }

    [Fact]
    public void TallHardwoodShield_HalvesChestAndAbdomenWeight_Provisional()
    {
        // PROVISIONAL gameplay tuning value, not a historical measurement.
        var rules = PhilippineCombatPreset.Rules;

        Assert.Equal(500, rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.Chest));
        Assert.Equal(500, rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.Abdomen));
        Assert.Equal(1_000, rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.Head));
        Assert.Equal(1_000, rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.WeaponArm));
        Assert.Equal(1_000, rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.Feet));
    }

    [Fact]
    public void RosterIsTheApprovedFourEntryConfiguration()
    {
        var roster = PhilippineCombatPreset.Rules.Roster;

        Assert.Equal(4, roster.Count);
        Assert.Equal(
            new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None),
            roster[0]);
        Assert.Equal(
            new CombatLoadout(WeaponId.HeavyChopper, ArmorId.LightOrganic, ShieldId.None),
            roster[1]);
        Assert.Equal(
            new CombatLoadout(WeaponId.ThrustingBlade, ArmorId.LightOrganic, ShieldId.TallHardwood),
            roster[2]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Bolo, ArmorId.LightOrganic, ShieldId.TallHardwood),
            roster[3]);
    }

    [Theory]
    [InlineData(1UL, 0)]
    [InlineData(2UL, 1)]
    [InlineData(3UL, 2)]
    [InlineData(4UL, 3)]
    [InlineData(5UL, 0)]
    [InlineData(8UL, 3)]
    [InlineData(9UL, 0)]
    public void ResolveLoadout_WrapsThroughTheRosterByEntityId(
        ulong entityId,
        int expectedRosterIndex)
    {
        var rules = PhilippineCombatPreset.Rules;

        Assert.Equal(
            rules.Roster[expectedRosterIndex],
            rules.ResolveLoadout(entityId));
    }

    [Fact]
    public void ResolveLoadout_RejectsEntityIdZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PhilippineCombatPreset.Rules.ResolveLoadout(0));
    }

    [Fact]
    public void PhilippinePresetV1_ContentHashMatchesTheApprovedGoldenValue()
    {
        Assert.Equal(0x59FB4CA563D87A49UL, PhilippineCombatPreset.Rules.ContentHash);
    }

    [Fact]
    public void Ruleset_CarriesTheNeutralClashProfileWhenGivenNone()
    {
        // The constructor parameter is optional so the named-argument
        // constructions elsewhere in this file keep compiling untouched.
        Assert.Same(ClashProfile.Neutral, PhilippineCombatPreset.Rules.ClashProfile);
    }

    [Fact]
    public void WithClashProfile_PreservesEveryFieldExceptTheProfile()
    {
        var source = PhilippineCombatPreset.Rules;
        var replacement = BuildDistinctClashProfile();

        var copy = source.WithClashProfile(replacement);

        Assert.Same(replacement, copy.ClashProfile);
        Assert.Equal(source.Id, copy.Id);
        Assert.Equal(source.Version, copy.Version);
        Assert.Equal(source.Roster, copy.Roster);

        foreach (var part in Enum.GetValues<BodyPart>())
        {
            Assert.Equal(
                source.GeneralTargets.Get(part),
                copy.GeneralTargets.Get(part));

            foreach (var weapon in Enum.GetValues<WeaponId>())
            {
                Assert.Equal(
                    source.ResolveWeaponWeight(weapon, part),
                    copy.ResolveWeaponWeight(weapon, part));
            }

            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                Assert.Equal(
                    source.ResolveDefenseMultiplier(shield, part),
                    copy.ResolveDefenseMultiplier(shield, part));
            }
        }

        // The only clause that reaches the armor set, which has no accessor
        // yet is folded into the content hash. A copy that dropped it would
        // move the hash. It also covers the targeting tables, which reach
        // simulation state through ResolveLoadout and HitLocationResolver, so
        // a copy that disturbed them would change hit locations and therefore
        // the ordered event stream, not merely a hash. Do not weaken it.
        Assert.Equal(
            source.ContentHash,
            source.WithClashProfile(source.ClashProfile).ContentHash);
    }

    [Fact]
    public void CombatPresetRegistry_ResolvesThePhilippinePreset()
    {
        var rules = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);

        Assert.Same(PhilippineCombatPreset.Rules, rules);
        Assert.True(CombatPresetRegistry.IsRegistered(CombatPresetId.PrecolonialPhilippinesV1));
    }

    [Fact]
    public void CombatPresetRegistry_RejectsUnregisteredPresetIds()
    {
        var unregistered = (CombatPresetId)999;

        Assert.False(CombatPresetRegistry.IsRegistered(unregistered));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CombatPresetRegistry.Get(unregistered));
    }

    [Fact]
    public void TargetWeightProfile_RejectsAnEmptyProfile()
    {
        Assert.Throws<ArgumentException>(
            () => new TargetWeightProfile([]));
    }

    [Fact]
    public void TargetWeightProfile_RejectsAProfileMissingAnEnumValue()
    {
        var entries = Enum.GetValues<BodyPart>()
            .Where(part => part != BodyPart.Feet)
            .Select(part => (part, 5))
            .ToArray();

        Assert.Throws<ArgumentException>(
            () => new TargetWeightProfile(entries));
    }

    [Fact]
    public void TargetWeightProfile_RejectsDuplicateBodyPartEntries()
    {
        var entries = Enum.GetValues<BodyPart>()
            .Where(part => part != BodyPart.Feet)
            .Select(part => (part, 5))
            .Append((BodyPart.Head, 5))
            .ToArray();

        Assert.Throws<ArgumentException>(
            () => new TargetWeightProfile(entries));
    }

    [Fact]
    public void TargetWeightProfile_RejectsNegativeWeights()
    {
        var entries = Enum.GetValues<BodyPart>()
            .Select(part => (part, part == BodyPart.Head ? -1 : 5))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TargetWeightProfile(entries));
    }

    [Fact]
    public void CombatRuleset_RejectsUnknownWeaponReferences()
    {
        var rules = BuildMinimalRuleset(
            weaponWeight: 5,
            shieldMultiplier: 1_000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => rules.ResolveWeaponWeight(WeaponId.Bolo, BodyPart.Head));
    }

    [Fact]
    public void CombatRuleset_RejectsUnknownShieldReferences()
    {
        var rules = BuildMinimalRuleset(
            weaponWeight: 5,
            shieldMultiplier: 1_000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => rules.ResolveDefenseMultiplier(ShieldId.TallHardwood, BodyPart.Head));
    }

    [Fact]
    public void CombatRuleset_RejectsAZeroResolvedTargetWeightTotal()
    {
        Assert.Throws<InvalidOperationException>(
            () => BuildMinimalRuleset(weaponWeight: 0, shieldMultiplier: 1_000));
    }

    [Fact]
    public void CombatRuleset_IsImmuneToPostConstructionMutationOfSuppliedCollections()
    {
        var uniformEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 5))
            .ToArray();
        var otherEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 9))
            .ToArray();
        var general = new TargetWeightProfile(uniformEntries);
        var weaponProfile = new TargetWeightProfile(uniformEntries);
        var shieldProfile = new TargetWeightProfile(uniformEntries);
        var otherWeaponProfile = new TargetWeightProfile(otherEntries);
        var otherShieldProfile = new TargetWeightProfile(otherEntries);

        var weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>
        {
            [WeaponId.GreatBlade] = weaponProfile,
        };
        var armors = new List<ArmorId> { ArmorId.LightOrganic };
        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = shieldProfile,
        };
        var roster = new List<CombatLoadout>
        {
            new(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None),
        };

        var rules = new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV1,
            version: 1,
            generalTargets: general,
            weaponTargets: weaponTargets,
            armors: armors,
            shieldMultipliers: shieldMultipliers,
            roster: roster);

        var hashBeforeMutation = rules.ContentHash;
        var weaponWeightBeforeMutation = rules.ResolveWeaponWeight(
            WeaponId.GreatBlade,
            BodyPart.Head);
        var defenseMultiplierBeforeMutation = rules.ResolveDefenseMultiplier(
            ShieldId.None,
            BodyPart.Head);
        var loadoutBeforeMutation = rules.ResolveLoadout(1);

        // Mutate every caller-supplied collection after construction. If
        // CombatRuleset kept these by reference, the ruleset's behavior
        // and ContentHash would silently drift apart.
        weaponTargets[WeaponId.HeavyChopper] = otherWeaponProfile;
        weaponTargets[WeaponId.GreatBlade] = otherWeaponProfile;
        shieldMultipliers[ShieldId.TallHardwood] = otherShieldProfile;
        shieldMultipliers[ShieldId.None] = otherShieldProfile;
        armors.Add(ArmorId.LightOrganic);
        roster.Add(new CombatLoadout(
            WeaponId.HeavyChopper,
            ArmorId.LightOrganic,
            ShieldId.TallHardwood));

        Assert.Equal(hashBeforeMutation, rules.ContentHash);
        Assert.Equal(
            weaponWeightBeforeMutation,
            rules.ResolveWeaponWeight(WeaponId.GreatBlade, BodyPart.Head));
        Assert.Equal(
            defenseMultiplierBeforeMutation,
            rules.ResolveDefenseMultiplier(ShieldId.None, BodyPart.Head));
        Assert.Equal(loadoutBeforeMutation, rules.ResolveLoadout(1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => rules.ResolveWeaponWeight(WeaponId.HeavyChopper, BodyPart.Head));
    }

    private static CombatRuleset BuildMinimalRuleset(int weaponWeight, int shieldMultiplier)
    {
        var uniformEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, weaponWeight))
            .ToArray();
        var uniformMultiplierEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, shieldMultiplier))
            .ToArray();

        var general = new TargetWeightProfile(uniformEntries);
        var weaponProfile = new TargetWeightProfile(uniformEntries);
        var shieldProfile = new TargetWeightProfile(uniformMultiplierEntries);

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV1,
            version: 1,
            generalTargets: general,
            weaponTargets: new Dictionary<WeaponId, TargetWeightProfile>
            {
                [WeaponId.GreatBlade] = weaponProfile,
            },
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = shieldProfile,
            },
            roster:
            [
                new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None),
            ]);
    }

    /// <summary>
    /// A profile whose every value differs from
    /// <see cref="ClashProfile.Neutral"/>, so a copy that quietly kept the
    /// original profile is visible rather than indistinguishable.
    /// </summary>
    private static ClashProfile BuildDistinctClashProfile()
    {
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = 100 + (int)attacker;
            }
        }

        var rows = weapons.ToDictionary(weapon => weapon, weapon => 100 + (int)weapon);

        return new ClashProfile(
            weaponIntercept: matrix,
            shieldIntercept: 1_111,
            voidChannel: rows,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }
}
