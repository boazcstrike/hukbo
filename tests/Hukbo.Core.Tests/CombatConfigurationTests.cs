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
    [InlineData(WeaponId.Kampilan, BodyPart.Head, 10)]
    [InlineData(WeaponId.Kampilan, BodyPart.Neck, 10)]
    [InlineData(WeaponId.Kampilan, BodyPart.Shoulder, 9)]
    [InlineData(WeaponId.Kampilan, BodyPart.WeaponArm, 8)]
    [InlineData(WeaponId.Kampilan, BodyPart.ShieldArm, 8)]
    [InlineData(WeaponId.Kampilan, BodyPart.Chest, 8)]
    [InlineData(WeaponId.Kampilan, BodyPart.Feet, 2)]
    [InlineData(WeaponId.Wasay, BodyPart.Shoulder, 10)]
    [InlineData(WeaponId.Wasay, BodyPart.Head, 9)]
    [InlineData(WeaponId.Wasay, BodyPart.WeaponArm, 9)]
    [InlineData(WeaponId.Wasay, BodyPart.ShieldArm, 9)]
    [InlineData(WeaponId.Wasay, BodyPart.Neck, 9)]
    [InlineData(WeaponId.Kalis, BodyPart.Abdomen, 10)]
    [InlineData(WeaponId.Kalis, BodyPart.Chest, 9)]
    [InlineData(WeaponId.Kalis, BodyPart.Neck, 8)]
    [InlineData(WeaponId.Kalis, BodyPart.WeaponArm, 10)]
    [InlineData(WeaponId.Itak, BodyPart.WeaponArm, 10)]
    [InlineData(WeaponId.Itak, BodyPart.ShieldArm, 10)]
    [InlineData(WeaponId.Itak, BodyPart.Hands, 9)]
    [InlineData(WeaponId.Itak, BodyPart.Neck, 8)]
    [InlineData(WeaponId.Itak, BodyPart.Face, 8)]
    [InlineData(WeaponId.Itak, BodyPart.Shoulder, 9)]
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
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            roster[0]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            roster[1]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
            roster[2]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
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
        // Re-baselined for preset version 2. The clash tables and the version
        // word are both folded into the content hash, so this value had to
        // move; it was replaced only after the two content-hash behaviour
        // tests, ContentHash_ChangesWhenAClashValueChanges and
        // ContentHash_IsIndependentOfClashDictionaryOrder, were passing. The
        // superseded value was 0x59FB4CA563D87A49UL.
        Assert.Equal(0x4EAFE27A42DE87B2UL, PhilippineCombatPreset.Rules.ContentHash);
    }

    [Fact]
    public void Ruleset_CarriesTheNeutralClashProfileWhenGivenNone()
    {
        // The constructor parameter is optional so the named-argument
        // constructions elsewhere in this file keep compiling untouched.
        //
        // The subject is a ruleset actually built without a clash profile.
        // While the preset carried none it was the obvious subject, but the
        // preset now supplies its own tables, so reading it here would assert
        // the exact opposite of what
        // Ruleset_DeclaresNonDefaultClashDataForEveryWeaponAndShield requires
        // of the same object. The property this test is named for — a ruleset
        // given no profile falls back to the neutral one — is unchanged and
        // still enforced; only the object exhibiting it moved.
        var givenNone = BuildMinimalRuleset(weaponWeight: 5, shieldMultiplier: 1_000);

        Assert.Same(ClashProfile.Neutral, givenNone.ClashProfile);
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

    /// <summary>
    /// Which clash value a <see cref="Preset_UsesApprovedClashValues"/> row
    /// pins. Public because the theory method that takes it has to be public
    /// for the test runner to see it.
    /// </summary>
    public enum ClashValueKind
    {
        WeaponIntercept,
        ShieldIntercept,
        Void,
        HardShareBase,
        HardShareMultiplier,
        MinimumHardShare,
        MaximumHardShare,
        MaximumInterception,
    }

    [Fact]
    public void Preset_ReportsVersionTwo()
    {
        // The clash tables are folded into the content hash, so the preset that
        // carries them is a different preset version. Section 3.6 of the design
        // keeps the preset identity and changes only its version.
        Assert.Equal(2, PhilippineCombatPreset.Rules.Version);
    }

    [Fact]
    public void Ruleset_DeclaresNonDefaultClashDataForEveryWeaponAndShield()
    {
        // Non-zero, not merely present. ClashProfile.Neutral is a complete
        // all-zero profile that answers every accessor, so a presence check
        // would pass against it and prove nothing.
        var profile = PhilippineCombatPreset.Rules.ClashProfile;

        foreach (var defender in Enum.GetValues<WeaponId>())
        {
            Assert.True(
                profile.ResolveVoid(defender) > 0,
                $"The void channel for defender {defender} is zero.");
            Assert.True(
                profile.ResolveHardShareBase(defender) > 0,
                $"The hard-share base for attacker {defender} is zero.");
            Assert.True(
                profile.ResolveHardShareMultiplier(defender) > 0,
                $"The hard-share multiplier for defender {defender} is zero.");

            foreach (var attacker in Enum.GetValues<WeaponId>())
            {
                Assert.True(
                    profile.ResolveWeaponIntercept(defender, attacker) > 0,
                    $"The weapon-intercept cell for defender {defender} " +
                    $"against attacker {attacker} is zero.");
            }
        }

        Assert.True(profile.ShieldInterceptBasisPoints > 0);
        Assert.True(profile.ResolveShieldIntercept(ShieldId.TallHardwood) > 0);
        Assert.Equal(0, profile.ResolveShieldIntercept(ShieldId.None));
        Assert.True(profile.MinimumHardShareBasisPoints > 0);
        Assert.True(
            profile.MaximumHardShareBasisPoints >
                profile.MinimumHardShareBasisPoints);
        Assert.True(profile.MaximumInterceptionBasisPoints > 0);
    }

    /// <summary>
    /// All thirty-two shipped tuning values, pinned one row each. Nothing else
    /// in the plan constrains a transcription error: the naive-reference sweep
    /// compares two implementations reading the <em>same</em> profile, so a
    /// wrong digit in any matrix cell is invisible to it.
    /// </summary>
    /// <remarks>
    /// <b>PROVISIONAL.</b> Every value here is a gameplay tuning choice, not a
    /// historical measurement. The research is explicit that all sixteen cells
    /// of the weapon-intercept matrix have no evidentiary confidence whatsoever;
    /// only their relative ordering is argued, and weakly.
    /// </remarks>
    [Theory]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.GreatBlade, WeaponId.GreatBlade, 2_200)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.GreatBlade, WeaponId.HeavyChopper, 1_900)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.GreatBlade, WeaponId.ThrustingBlade, 1_600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.GreatBlade, WeaponId.Bolo, 2_000)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.HeavyChopper, WeaponId.GreatBlade, 1_500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.HeavyChopper, WeaponId.HeavyChopper, 1_300)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.HeavyChopper, WeaponId.ThrustingBlade, 1_100)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.HeavyChopper, WeaponId.Bolo, 1_400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.ThrustingBlade, WeaponId.GreatBlade, 500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.ThrustingBlade, WeaponId.HeavyChopper, 400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.ThrustingBlade, WeaponId.ThrustingBlade, 600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.ThrustingBlade, WeaponId.Bolo, 600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Bolo, WeaponId.GreatBlade, 400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Bolo, WeaponId.HeavyChopper, 300)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Bolo, WeaponId.ThrustingBlade, 500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Bolo, WeaponId.Bolo, 500)]
    [InlineData(ClashValueKind.ShieldIntercept, WeaponId.GreatBlade, WeaponId.GreatBlade, 2_400)]
    [InlineData(ClashValueKind.Void, WeaponId.GreatBlade, WeaponId.GreatBlade, 1_000)]
    [InlineData(ClashValueKind.Void, WeaponId.HeavyChopper, WeaponId.GreatBlade, 900)]
    [InlineData(ClashValueKind.Void, WeaponId.ThrustingBlade, WeaponId.GreatBlade, 1_000)]
    [InlineData(ClashValueKind.Void, WeaponId.Bolo, WeaponId.GreatBlade, 1_100)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.GreatBlade, WeaponId.GreatBlade, 3_300)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.GreatBlade, WeaponId.HeavyChopper, 4_000)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.GreatBlade, WeaponId.ThrustingBlade, 1_200)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.GreatBlade, WeaponId.Bolo, 1_800)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.GreatBlade, WeaponId.GreatBlade, 1_150)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.HeavyChopper, WeaponId.GreatBlade, 1_050)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.ThrustingBlade, WeaponId.GreatBlade, 750)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.Bolo, WeaponId.GreatBlade, 700)]
    [InlineData(ClashValueKind.MinimumHardShare, WeaponId.GreatBlade, WeaponId.GreatBlade, 500)]
    [InlineData(ClashValueKind.MaximumHardShare, WeaponId.GreatBlade, WeaponId.GreatBlade, 6_000)]
    [InlineData(ClashValueKind.MaximumInterception, WeaponId.GreatBlade, WeaponId.GreatBlade, 5_500)]
    public void Preset_UsesApprovedClashValues(
        ClashValueKind kind,
        WeaponId defenderWeapon,
        WeaponId attackerWeapon,
        int expected)
    {
        var profile = PhilippineCombatPreset.Rules.ClashProfile;
        var actual = kind switch
        {
            ClashValueKind.WeaponIntercept =>
                profile.ResolveWeaponIntercept(defenderWeapon, attackerWeapon),
            ClashValueKind.ShieldIntercept => profile.ShieldInterceptBasisPoints,
            ClashValueKind.Void => profile.ResolveVoid(defenderWeapon),
            ClashValueKind.HardShareBase =>
                profile.ResolveHardShareBase(attackerWeapon),
            ClashValueKind.HardShareMultiplier =>
                profile.ResolveHardShareMultiplier(defenderWeapon),
            ClashValueKind.MinimumHardShare => profile.MinimumHardShareBasisPoints,
            ClashValueKind.MaximumHardShare => profile.MaximumHardShareBasisPoints,
            _ => profile.MaximumInterceptionBasisPoints,
        };

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The cheapest possible check that the tables were entered as designed: the
    /// four row means of the total-interception matrix in design section 3.3.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.GreatBlade, ShieldId.None, 2_925)]
    [InlineData(WeaponId.HeavyChopper, ShieldId.None, 2_225)]
    [InlineData(WeaponId.ThrustingBlade, ShieldId.TallHardwood, 3_925)]
    [InlineData(WeaponId.Bolo, ShieldId.TallHardwood, 3_925)]
    public void Preset_RowMeansMatchTheDesignedTotalInterceptionMatrix(
        WeaponId defenderWeapon,
        ShieldId defenderShield,
        int expectedMean)
    {
        var profile = PhilippineCombatPreset.Rules.ClashProfile;
        var attackers = Enum.GetValues<WeaponId>();
        var total = 0;

        foreach (var attacker in attackers)
        {
            total +=
                profile.ResolveShieldIntercept(defenderShield) +
                profile.ResolveWeaponIntercept(defenderWeapon, attacker) +
                profile.ResolveVoid(defenderWeapon);
        }

        // Compared as a sum so that an exact expectation is possible without
        // arguing about how a mean should round.
        Assert.Equal(expectedMean * attackers.Length, total);
        Assert.Equal(expectedMean, total / attackers.Length);
    }

    [Fact]
    public void ContentHash_ChangesWhenAClashValueChanges()
    {
        var preset = PhilippineCombatPreset.Rules;
        var baseline = preset.WithClashProfile(BuildUniformClashProfile(matrixCell: 1_000));
        var oneCellChanged =
            preset.WithClashProfile(BuildUniformClashProfile(matrixCell: 1_001));

        Assert.NotEqual(baseline.ContentHash, oneCellChanged.ContentHash);
        Assert.NotEqual(
            baseline.ContentHash,
            preset.WithClashProfile(ClashProfile.Neutral).ContentHash);
    }

    [Fact]
    public void ContentHash_IsIndependentOfClashDictionaryOrder()
    {
        // Identical values supplied in opposite key order. A fold that inherited
        // the caller's dictionary order would give two equivalent rulesets two
        // different content hashes, and a replay would refuse a save that is in
        // fact the same configuration.
        var preset = PhilippineCombatPreset.Rules;
        var ascending = preset.WithClashProfile(
            BuildOrderedClashProfile(reversed: false));
        var descending = preset.WithClashProfile(
            BuildOrderedClashProfile(reversed: true));

        Assert.Equal(ascending.ContentHash, descending.ContentHash);
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
            () => rules.ResolveWeaponWeight(WeaponId.Itak, BodyPart.Head));
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
            [WeaponId.Kampilan] = weaponProfile,
        };
        var armors = new List<ArmorId> { ArmorId.LightOrganic };
        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = shieldProfile,
        };
        var roster = new List<CombatLoadout>
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
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
            WeaponId.Kampilan,
            BodyPart.Head);
        var defenseMultiplierBeforeMutation = rules.ResolveDefenseMultiplier(
            ShieldId.None,
            BodyPart.Head);
        var loadoutBeforeMutation = rules.ResolveLoadout(1);

        // Mutate every caller-supplied collection after construction. If
        // CombatRuleset kept these by reference, the ruleset's behavior
        // and ContentHash would silently drift apart.
        weaponTargets[WeaponId.Wasay] = otherWeaponProfile;
        weaponTargets[WeaponId.Kampilan] = otherWeaponProfile;
        shieldMultipliers[ShieldId.TallHardwood] = otherShieldProfile;
        shieldMultipliers[ShieldId.None] = otherShieldProfile;
        armors.Add(ArmorId.LightOrganic);
        roster.Add(new CombatLoadout(
            WeaponId.Wasay,
            ArmorId.LightOrganic,
            ShieldId.TallHardwood));

        Assert.Equal(hashBeforeMutation, rules.ContentHash);
        Assert.Equal(
            weaponWeightBeforeMutation,
            rules.ResolveWeaponWeight(WeaponId.Kampilan, BodyPart.Head));
        Assert.Equal(
            defenseMultiplierBeforeMutation,
            rules.ResolveDefenseMultiplier(ShieldId.None, BodyPart.Head));
        Assert.Equal(loadoutBeforeMutation, rules.ResolveLoadout(1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => rules.ResolveWeaponWeight(WeaponId.Wasay, BodyPart.Head));
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
                [WeaponId.Kampilan] = weaponProfile,
            },
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = shieldProfile,
            },
            roster:
            [
                new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            ]);
    }

    /// <summary>
    /// A profile whose tables are uniform apart from one matrix cell value, so
    /// two calls differing only in <paramref name="matrixCell"/> differ in
    /// exactly sixteen folded words and nothing else.
    /// </summary>
    private static ClashProfile BuildUniformClashProfile(int matrixCell)
    {
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = matrixCell;
            }
        }

        var rows = weapons.ToDictionary(weapon => weapon, _ => 500);

        return new ClashProfile(
            matrix,
            shieldIntercept: 2_400,
            voidChannel: rows,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// One set of clash values supplied to the constructor in ascending or
    /// descending key order. The two profiles are equal in content and differ
    /// only in the order the dictionaries were populated.
    /// </summary>
    private static ClashProfile BuildOrderedClashProfile(bool reversed)
    {
        var weapons = Enum.GetValues<WeaponId>().OrderBy(weapon => (int)weapon).ToArray();
        if (reversed)
        {
            weapons = [.. weapons.Reverse()];
        }

        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        var voidChannel = new Dictionary<WeaponId, int>();
        var hardShareBases = new Dictionary<WeaponId, int>();
        var hardShareMultipliers = new Dictionary<WeaponId, int>();

        foreach (var defender in weapons)
        {
            voidChannel[defender] = 900 + (int)defender;
            hardShareBases[defender] = 1_200 + (int)defender;
            hardShareMultipliers[defender] = 700 + (int)defender;

            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] =
                    (((int)defender * 10) + (int)attacker) * 10;
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: hardShareBases,
            hardShareMultipliers: hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
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
