using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;

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
    public void PhilippinePresetV1_ContentHashStaysAtTheFrozenGoldenValue()
    {
        // D1/D2 regression guard. Preset V1 is frozen: it declares no weapon
        // attributes and no clash profile, so neither block is folded into
        // its content hash -- not even a zero count -- and this value must
        // never move. If it does, the conditional fold in
        // CombatRuleset.ComputeContentHash is broken. Preset V2 carries the
        // clash tables instead; see
        // PhilippineCombatIntegrationTests for its content hash.
        Assert.Equal(0x59FB4CA563D87A49UL, PhilippineCombatPreset.Rules.ContentHash);
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
        // D2: the clash profile folds into the content hash only when one was
        // supplied. PhilippineCombatPreset (V1) declares none, so the final
        // assertion below -- round-tripping source.ClashProfile through
        // WithClashProfile and expecting the same hash -- would turn V1's
        // undeclared (Neutral fallback) profile into an explicitly declared
        // one and legitimately move the hash. PhilippineCombatPresetV2
        // already declares a profile, so the round trip changes nothing.
        var source = PhilippineCombatPresetV2.Rules;
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

            // Scoped to the weapons this source ruleset actually declares
            // target weights for, not the bare WeaponId enum: PhilippineCombatPresetV2
            // is a frozen four-melee-weapon preset and never gains the three
            // ranged weapons WeaponId later added, so a full-enum sweep would
            // throw on a weapon this preset was never asked to know about.
            foreach (var weapon in source.Roster
                .Select(loadout => loadout.Weapon)
                .Distinct()
                .OrderBy(id => (int)id))
            {
                Assert.Equal(
                    source.ResolveWeaponWeight(weapon, part),
                    copy.ResolveWeaponWeight(weapon, part));
            }

            // Scoped to the shields this source ruleset actually declares, for
            // exactly the reason the weapon sweep above is scoped: a frozen
            // preset never gains a shield ShieldId adds later, so a full-enum
            // sweep would throw on a shield this preset was never asked to
            // know about.
            foreach (var shield in source.Roster
                .Select(loadout => loadout.Shield)
                .Distinct()
                .OrderBy(id => (int)id))
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
    public void PresetV1_StaysAtVersionOne()
    {
        // D1: preset V1 is frozen and carries no clash profile at all. New
        // clash behaviour lives on PhilippineCombatPresetV2, which is what
        // Scenario.CombatPreset defaults to.
        Assert.Equal(1, PhilippineCombatPreset.Rules.Version);
        Assert.Same(ClashProfile.Neutral, PhilippineCombatPreset.Rules.ClashProfile);
    }

    [Fact]
    public void PresetV2_DeclaresNonDefaultClashDataForEveryRosterLoadout()
    {
        // Non-zero, not merely present. ClashProfile.Neutral is a complete
        // all-zero profile that answers every accessor, so a presence check
        // would pass against it and prove nothing. Iterated over the actual
        // six-loadout roster rather than the bare WeaponId enum, because D3
        // keys the weapon-intercept and void tables on (weapon, shield) and a
        // solo and a shield-paired loadout of the same weapon carry
        // materially different values.
        var rules = PhilippineCombatPresetV2.Rules;
        var profile = rules.ClashProfile;

        foreach (var defender in rules.Roster)
        {
            Assert.True(
                profile.ResolveVoid(defender.Weapon, defender.Shield) > 0,
                $"The void channel for defender {defender.Weapon}/{defender.Shield} is zero.");
            Assert.True(
                profile.ResolveHardShareBase(defender.Weapon) > 0,
                $"The hard-share base for attacker {defender.Weapon} is zero.");
            Assert.True(
                profile.ResolveHardShareMultiplier(defender.Weapon) > 0,
                $"The hard-share multiplier for defender {defender.Weapon} is zero.");

            foreach (var attacker in rules.Roster)
            {
                Assert.True(
                    profile.ResolveWeaponIntercept(
                        defender.Weapon,
                        defender.Shield,
                        attacker.Weapon) > 0,
                    $"The weapon-intercept cell for defender {defender.Weapon}/" +
                    $"{defender.Shield} against attacker {attacker.Weapon} is zero.");
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
    /// The shipped tuning values for preset V2, pinned one row each against
    /// the three-part (defender weapon, defender shield, attacker weapon) key.
    /// Nothing else in the plan constrains a transcription error: the
    /// naive-reference sweep compares two implementations reading the
    /// <em>same</em> profile, so a wrong digit in any matrix cell is invisible
    /// to it.
    /// </summary>
    /// <remarks>
    /// <b>PROVISIONAL.</b> Every value here is a gameplay tuning choice, not a
    /// historical measurement. The research is explicit that the sixteen
    /// legacy cells of the weapon-intercept matrix have no evidentiary
    /// confidence whatsoever; only their relative ordering is argued, and
    /// weakly. The ten new cells (shieldless Kalis and shieldless Itak) are
    /// likewise provisional reconstructions; see
    /// PhilippineCombatPresetV2.BuildClashProfile.
    /// </remarks>
    [Theory]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 2_200)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kampilan, ShieldId.None, WeaponId.Wasay, 1_900)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kampilan, ShieldId.None, WeaponId.Kalis, 1_600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kampilan, ShieldId.None, WeaponId.Itak, 2_000)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Wasay, ShieldId.None, WeaponId.Kampilan, 1_500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Wasay, ShieldId.None, WeaponId.Wasay, 1_300)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Wasay, ShieldId.None, WeaponId.Kalis, 1_100)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Wasay, ShieldId.None, WeaponId.Itak, 1_400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kampilan, 500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Wasay, 400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kalis, 600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Itak, 600)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kampilan, 400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Wasay, 300)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kalis, 500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Itak, 500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.None, WeaponId.Kampilan, 1_200)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.None, WeaponId.Wasay, 1_000)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.None, WeaponId.Kalis, 1_500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Kalis, ShieldId.None, WeaponId.Itak, 1_500)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.None, WeaponId.Kampilan, 1_100)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.None, WeaponId.Wasay, 1_000)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.None, WeaponId.Kalis, 1_400)]
    [InlineData(ClashValueKind.WeaponIntercept, WeaponId.Itak, ShieldId.None, WeaponId.Itak, 1_400)]
    [InlineData(ClashValueKind.ShieldIntercept, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 2_400)]
    [InlineData(ClashValueKind.Void, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 1_000)]
    [InlineData(ClashValueKind.Void, WeaponId.Wasay, ShieldId.None, WeaponId.Kampilan, 900)]
    [InlineData(ClashValueKind.Void, WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kampilan, 1_000)]
    [InlineData(ClashValueKind.Void, WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kampilan, 1_100)]
    [InlineData(ClashValueKind.Void, WeaponId.Kalis, ShieldId.None, WeaponId.Kampilan, 1_350)]
    [InlineData(ClashValueKind.Void, WeaponId.Itak, ShieldId.None, WeaponId.Kampilan, 1_450)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 3_300)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.Kampilan, ShieldId.None, WeaponId.Wasay, 4_000)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.Kampilan, ShieldId.None, WeaponId.Kalis, 1_200)]
    [InlineData(ClashValueKind.HardShareBase, WeaponId.Kampilan, ShieldId.None, WeaponId.Itak, 1_800)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 1_150)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.Wasay, ShieldId.None, WeaponId.Kampilan, 1_050)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.Kalis, ShieldId.None, WeaponId.Kampilan, 750)]
    [InlineData(ClashValueKind.HardShareMultiplier, WeaponId.Itak, ShieldId.None, WeaponId.Kampilan, 700)]
    [InlineData(ClashValueKind.MinimumHardShare, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 500)]
    [InlineData(ClashValueKind.MaximumHardShare, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 6_000)]
    [InlineData(ClashValueKind.MaximumInterception, WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan, 5_500)]
    public void PresetV2_UsesApprovedClashValues(
        ClashValueKind kind,
        WeaponId defenderWeapon,
        ShieldId defenderShield,
        WeaponId attackerWeapon,
        int expected)
    {
        var profile = PhilippineCombatPresetV2.Rules.ClashProfile;
        var actual = kind switch
        {
            ClashValueKind.WeaponIntercept =>
                profile.ResolveWeaponIntercept(defenderWeapon, defenderShield, attackerWeapon),
            ClashValueKind.ShieldIntercept => profile.ShieldInterceptBasisPoints,
            ClashValueKind.Void => profile.ResolveVoid(defenderWeapon, defenderShield),
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
    /// The cheapest possible check that the six roster loadouts' tables were
    /// entered as designed: the row means of the total-interception matrix,
    /// keyed by (defender weapon, defender shield).
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None, 2_925)]
    [InlineData(WeaponId.Wasay, ShieldId.None, 2_225)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, 3_925)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, 3_925)]
    [InlineData(WeaponId.Kalis, ShieldId.None, 2_650)]
    [InlineData(WeaponId.Itak, ShieldId.None, 2_675)]
    public void PresetV2_RowMeansMatchTheDesignedTotalInterceptionMatrix(
        WeaponId defenderWeapon,
        ShieldId defenderShield,
        int expectedMean)
    {
        var profile = PhilippineCombatPresetV2.Rules.ClashProfile;

        // Scoped to the weapons PhilippineCombatPresetV2's roster actually
        // fields, not the bare WeaponId enum: this frozen preset only ever
        // declares clash data for its original four melee weapons, and the
        // pinned expectedMean values above were computed against exactly
        // those four attackers, before WeaponId later gained three ranged
        // members this preset never learns about.
        var attackers = PhilippineCombatPresetV2.Rules.Roster
            .Select(loadout => loadout.Weapon)
            .Distinct()
            .OrderBy(id => (int)id)
            .ToArray();
        var total = 0;

        foreach (var attacker in attackers)
        {
            total +=
                profile.ResolveShieldIntercept(defenderShield) +
                profile.ResolveWeaponIntercept(defenderWeapon, defenderShield, attacker) +
                profile.ResolveVoid(defenderWeapon, defenderShield);
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
            BuildThreePartKeyedClashProfile(reversed: false));
        var descending = preset.WithClashProfile(
            BuildThreePartKeyedClashProfile(reversed: true));

        Assert.Equal(ascending.ContentHash, descending.ContentHash);
    }

    /// <summary>
    /// T41A / D3.1. Proves the fold reduced from D3's re-key still
    /// distinguishes a shielded defender from a bare one, and that it stays
    /// independent of dictionary insertion order for the three-part key.
    /// Without this case the T13A hole -- <see cref="CombatRuleset.FoldClashProfile"/>
    /// dropping <c>DefenderShield</c> from the folded bytes -- reopens the
    /// first time somebody "simplifies" the comparator, and two profiles
    /// differing only in whether one cell describes a shielded or a bare
    /// defender would hash identically: a save or replay would then accept a
    /// materially different configuration as the same one.
    /// </summary>
    [Fact]
    public void ContentHash_DistinguishesAShieldedDefenderCellFromABareDefenderCell()
    {
        var preset = PhilippineCombatPreset.Rules;
        var shieldedDefenderCell = preset.WithClashProfile(
            BuildSingleCellClashProfile(
                defenderShield: ShieldId.TallHardwood,
                matrixCell: 1_234));
        var bareDefenderCell = preset.WithClashProfile(
            BuildSingleCellClashProfile(
                defenderShield: ShieldId.None,
                matrixCell: 1_234));

        Assert.NotEqual(shieldedDefenderCell.ContentHash, bareDefenderCell.ContentHash);
    }

    /// <summary>
    /// T41A / D3.1, the insertion-order half. Same three-part-keyed cells,
    /// supplied to the dictionary in two different orders, must hash
    /// identically.
    /// </summary>
    [Fact]
    public void ContentHash_IsIndependentOfInsertionOrderForTheThreePartKey()
    {
        var preset = PhilippineCombatPreset.Rules;
        var forward = preset.WithClashProfile(
            BuildThreePartKeyedClashProfile(reversed: false));
        var reversedOrder = preset.WithClashProfile(
            BuildThreePartKeyedClashProfile(reversed: true));

        Assert.Equal(forward.ContentHash, reversedOrder.ContentHash);
    }

    [Fact]
    public void RankLevels_RejectsALevelBelowOne()
    {
        Assert.Throws<ArgumentException>(
            () => BuildRulesetWithRankLevels(
                new Dictionary<RankId, int> { [RankId.Timawa] = 0 }));
    }

    [Fact]
    public void RankLevels_RejectsARosterRankWithNoDeclaredLevel()
    {
        Assert.Throws<ArgumentException>(
            () => BuildRulesetWithRankLevels(
                new Dictionary<RankId, int> { [RankId.Datu] = 3 },
                rosterRank: RankId.Timawa));
    }

    [Fact]
    public void ContentHash_IsIndependentOfRankLevelDictionaryOrder()
    {
        // R4's load-bearing determinism check: identical rank-level data
        // supplied in opposite key order must hash identically, exactly as
        // the clash-profile and weapon-attribute dictionaries above already
        // require.
        var ascending = BuildRulesetWithRankLevels(new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Timawa] = 2,
        });
        var descending = BuildRulesetWithRankLevels(new Dictionary<RankId, int>
        {
            [RankId.Timawa] = 2,
            [RankId.Datu] = 3,
        });

        Assert.Equal(ascending.ContentHash, descending.ContentHash);
    }

    /// <summary>
    /// RU-36 / D3.1's ranged hole. Before this fix, <c>AddProfile</c> folded
    /// only <c>DamagePerAttack</c>, <c>AttackRangeRaw</c>, and
    /// <c>AttackCooldownTicks</c>, so a preset whose only difference was one
    /// of the three ranged fields hashed identically to the profile it
    /// diverged from — a replay recorded against the old tuning would then be
    /// accepted and diverge. This is the direct regression test: a ranged
    /// profile and the same profile with all three ranged fields zeroed out
    /// (a melee no-op declaration) must hash differently.
    /// </summary>
    [Fact]
    public void ContentHash_DiffersBetweenARangedProfileAndTheSameProfileWithRangedFieldsZeroed()
    {
        var ranged = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 15,
                    reachWorldUnits: 16,
                    cooldownTicks: 7,
                    projectileSpeedWorldUnits: 4,
                    standoffWorldUnits: 12,
                    flightTickCeiling: 30)),
        });
        var zeroed = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                MeleeProfile(damage: 15, reachWorldUnits: 16, cooldownTicks: 7)),
        });

        Assert.NotEqual(ranged.ContentHash, zeroed.ContentHash);
    }

    /// <summary>
    /// RU-36: proves all three ranged fields are folded, not just one. Each
    /// case below holds two of the three ranged fields fixed and changes only
    /// the third; a fold that dropped a field would leave the case that
    /// varies exactly that field passing by accident while looking correct.
    /// </summary>
    [Fact]
    public void ContentHash_ChangesWhenAnyOneRangedFieldChangesWithTheOtherTwoHeld()
    {
        var baseline = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                RangedProfile(15, 16, 7, projectileSpeedWorldUnits: 4, standoffWorldUnits: 12, flightTickCeiling: 30)),
        });
        var projectileSpeedChanged = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                RangedProfile(15, 16, 7, projectileSpeedWorldUnits: 5, standoffWorldUnits: 12, flightTickCeiling: 30)),
        });
        var standoffDistanceChanged = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                RangedProfile(15, 16, 7, projectileSpeedWorldUnits: 4, standoffWorldUnits: 13, flightTickCeiling: 30)),
        });
        var flightTickCeilingChanged = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                RangedProfile(15, 16, 7, projectileSpeedWorldUnits: 4, standoffWorldUnits: 12, flightTickCeiling: 31)),
        });

        Assert.NotEqual(baseline.ContentHash, projectileSpeedChanged.ContentHash);
        Assert.NotEqual(baseline.ContentHash, standoffDistanceChanged.ContentHash);
        Assert.NotEqual(baseline.ContentHash, flightTickCeilingChanged.ContentHash);
    }

    /// <summary>
    /// RU-36: the new conditional ranged-field fold must not reopen order
    /// dependence for a fully melee weapon-attribute table — the same
    /// guarantee <see cref="ContentHash_IsIndependentOfRankLevelDictionaryOrder"/>
    /// already holds for rank levels.
    /// </summary>
    [Fact]
    public void ContentHash_IsIndependentOfWeaponAttributeDictionaryOrderWhenBothWeaponsAreMelee()
    {
        var kampilan = WeaponAttributes.TwoHanded(
            MeleeProfile(damage: 15, reachWorldUnits: 16, cooldownTicks: 7));
        var wasay = WeaponAttributes.TwoHanded(
            MeleeProfile(damage: 18, reachWorldUnits: 13, cooldownTicks: 8));

        var ascending = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = kampilan,
            [WeaponId.Wasay] = wasay,
        });
        var descending = BuildRulesetWithWeaponAttributes(new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Wasay] = wasay,
            [WeaponId.Kampilan] = kampilan,
        });

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
    /// A single-weapon ruleset like <see cref="BuildMinimalRuleset"/> above,
    /// but declaring <paramref name="rankLevels"/> and fielding
    /// <paramref name="rosterRank"/> on its one roster entry, for the
    /// rank-level validation and content-hash tests.
    /// </summary>
    private static CombatRuleset BuildRulesetWithRankLevels(
        IReadOnlyDictionary<RankId, int> rankLevels,
        RankId rosterRank = RankId.Timawa)
    {
        var uniformEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 5))
            .ToArray();
        var uniformMultiplierEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1_000))
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
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.None,
                    rosterRank),
            ],
            rankLevels: rankLevels);
    }

    /// <summary>
    /// RU-36. A ruleset carrying <paramref name="weaponAttributes"/> as its
    /// only interesting content: one uniform target-weight profile shared by
    /// every declared weapon, and a one-entry roster fielding whichever
    /// declared weapon sorts first by <see cref="WeaponId"/>. Every declared
    /// weapon is two-handed in every caller of this helper, so
    /// <c>ShieldId.None</c> never trips the two-handed-plus-shield
    /// invariant regardless of which weapon the roster picks.
    /// </summary>
    private static CombatRuleset BuildRulesetWithWeaponAttributes(
        IReadOnlyDictionary<WeaponId, WeaponAttributes> weaponAttributes)
    {
        var uniformEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 5))
            .ToArray();
        var uniformMultiplierEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1_000))
            .ToArray();

        var general = new TargetWeightProfile(uniformEntries);
        var weaponProfile = new TargetWeightProfile(uniformEntries);
        var shieldProfile = new TargetWeightProfile(uniformMultiplierEntries);
        var weaponTargets = weaponAttributes.Keys
            .ToDictionary(weapon => weapon, _ => weaponProfile);
        var rosterWeapon = weaponAttributes.Keys.OrderBy(id => (int)id).First();

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV1,
            version: 1,
            generalTargets: general,
            weaponTargets: weaponTargets,
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = shieldProfile,
            },
            roster:
            [
                new CombatLoadout(rosterWeapon, ArmorId.LightOrganic, ShieldId.None),
            ],
            weaponAttributes: weaponAttributes);
    }

    /// <summary>
    /// A melee profile: all three ranged fields left at their zero default,
    /// matching <see cref="WeaponProfile.ValidateRangedFields"/>'s melee
    /// no-op declaration.
    /// </summary>
    private static WeaponProfile MeleeProfile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks) =>
        new(damage, reachWorldUnits * FixedPoint.Scale, cooldownTicks);

    /// <summary>
    /// A ranged profile declaring all three ranged fields, with the standoff
    /// distance validated by the constructor to sit strictly inside the
    /// reach every caller here supplies.
    /// </summary>
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

    /// <summary>
    /// A profile whose tables are uniform apart from one matrix cell value, so
    /// two calls differing only in <paramref name="matrixCell"/> differ in
    /// exactly one folded cell and nothing else. Every (weapon, shield,
    /// weapon) triple is populated, which is a superset of any roster's
    /// coverage requirement.
    /// </summary>
    private static ClashProfile BuildUniformClashProfile(int matrixCell)
    {
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var defenderShield in shields)
            {
                foreach (var attacker in weapons)
                {
                    matrix[(defender, defenderShield, attacker)] = matrixCell;
                }
            }
        }

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var weapon in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(weapon, shield)] = 500;
            }
        }

        var rows = weapons.ToDictionary(weapon => weapon, _ => 500);

        return new ClashProfile(
            matrix,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// T41A. A full cross-product profile uniform at 500 everywhere, except
    /// one weapon-intercept cell for defender Kalis against attacker Kampilan,
    /// which is set to <paramref name="matrixCell"/> under
    /// <paramref name="defenderShield"/>. Two calls with the same
    /// <paramref name="matrixCell"/> but a different
    /// <paramref name="defenderShield"/> are identical in every other cell, so
    /// a hash difference between them proves the fold folds the defender's
    /// shield rather than dropping it.
    /// </summary>
    private static ClashProfile BuildSingleCellClashProfile(
        ShieldId defenderShield,
        int matrixCell)
    {
        var profile = BuildUniformClashProfile(500);
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var shield in shields)
            {
                foreach (var attacker in weapons)
                {
                    matrix[(defender, shield, attacker)] =
                        defender == WeaponId.Kalis &&
                            shield == defenderShield &&
                            attacker == WeaponId.Kampilan
                            ? matrixCell
                            : 500;
                }
            }
        }

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var weapon in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(weapon, shield)] = 500;
            }
        }

        var rows = weapons.ToDictionary(weapon => weapon, _ => 500);

        return new ClashProfile(
            matrix,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// One set of clash values, keyed by the full three-part
    /// (defender weapon, defender shield, attacker weapon) key, supplied to
    /// the constructor in ascending or descending key order. The two profiles
    /// are equal in content and differ only in the order the dictionaries
    /// were populated.
    /// </summary>
    private static ClashProfile BuildThreePartKeyedClashProfile(bool reversed)
    {
        var weapons = Enum.GetValues<WeaponId>().OrderBy(weapon => (int)weapon).ToArray();
        var shields = Enum.GetValues<ShieldId>().OrderBy(shield => (int)shield).ToArray();
        if (reversed)
        {
            weapons = [.. weapons.Reverse()];
            shields = [.. shields.Reverse()];
        }

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        var hardShareBases = new Dictionary<WeaponId, int>();
        var hardShareMultipliers = new Dictionary<WeaponId, int>();

        foreach (var defender in weapons)
        {
            hardShareBases[defender] = 1_200 + (int)defender;
            hardShareMultipliers[defender] = 700 + (int)defender;

            foreach (var defenderShield in shields)
            {
                voidChannel[(defender, defenderShield)] =
                    900 + ((int)defender * 10) + (int)defenderShield;

                foreach (var attacker in weapons)
                {
                    matrix[(defender, defenderShield, attacker)] =
                        ((int)defender * 1_000) +
                        ((int)defenderShield * 100) +
                        ((int)attacker * 10);
                }
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
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var defenderShield in shields)
            {
                foreach (var attacker in weapons)
                {
                    matrix[(defender, defenderShield, attacker)] = 100 + (int)attacker;
                }
            }
        }

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var weapon in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(weapon, shield)] = 100 + (int)weapon;
            }
        }

        var rows = weapons.ToDictionary(weapon => weapon, weapon => 100 + (int)weapon);

        return new ClashProfile(
            weaponIntercept: matrix,
            shieldIntercept: 1_111,
            voidChannel: voidChannel,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }
}
