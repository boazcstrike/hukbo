using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

/// <summary>
/// Covers the per-(<see cref="ArmorId"/>, <see cref="BodyPart"/>)
/// damage-mitigation table on <see cref="CombatRuleset"/>: the two
/// construction-time invariants (total coverage of every declared
/// <see cref="ArmorId"/> member, and every basis-point value staying inside
/// <c>[0, 10000]</c>), the resolve and apply accessors, and
/// <see cref="PhilippineCombatPresetV8"/>'s own declared table and roster.
/// </summary>
public sealed class ArmorMitigationTests
{
    /// <summary>
    /// The expected mitigation table for <see cref="PhilippineCombatPresetV8"/>,
    /// restated independently from the armor identity design document section
    /// 6.1 rather than read back from the preset, so a change to the preset's
    /// own table that drifts from the design is caught here rather than
    /// merely reproduced.
    /// </summary>
    private static readonly IReadOnlyDictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>> ExpectedV7Mitigation =
        new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>
        {
            [ArmorId.None] = MitigationRow(0, 0, 0, 0),
            [ArmorId.LightOrganic] = MitigationRow(0, 0, 0, 0),
            [ArmorId.QuiltedCotton] = MitigationRow(1_200, 600, 600, 0),
            [ArmorId.HideCorselet] = MitigationRow(1_800, 0, 0, 0),
            [ArmorId.RigidCuirass] = MitigationRow(2_500, 0, 0, 0),
            [ArmorId.ImportedIron] = MitigationRow(3_200, 1_600, 1_600, 0),
        };

    private static IReadOnlyDictionary<BodyPart, int> MitigationRow(
        int torso, int arms, int legs, int head) =>
        new Dictionary<BodyPart, int>
        {
            [BodyPart.Chest] = torso,
            [BodyPart.Abdomen] = torso,
            [BodyPart.Shoulder] = torso,
            [BodyPart.WeaponArm] = arms,
            [BodyPart.ShieldArm] = arms,
            [BodyPart.Hands] = arms,
            [BodyPart.Thigh] = legs,
            [BodyPart.Knee] = legs,
            [BodyPart.Shin] = legs,
            [BodyPart.Feet] = legs,
            [BodyPart.Head] = head,
            [BodyPart.Neck] = head,
            [BodyPart.Face] = head,
        };

    [Fact]
    public void PresetV8_ResolvesTheDesignedMitigationValue_ForEveryArmorAndBodyPart()
    {
        var rules = PhilippineCombatPresetV8.Rules;

        Assert.True(rules.HasArmorMitigation);

        foreach (var armor in Enum.GetValues<ArmorId>())
        {
            foreach (var part in Enum.GetValues<BodyPart>())
            {
                var expected = ExpectedV7Mitigation[armor][part];
                var actual = rules.ResolveArmorMitigation(armor, part);
                Assert.True(
                    expected == actual,
                    $"Expected {expected} basis points for {armor}/{part}, " +
                    $"got {actual}.");
            }
        }
    }

    [Fact]
    public void ApplyArmorMitigation_ReducesDamageByTheDeclaredBasisPoints()
    {
        var rules = PhilippineCombatPresetV8.Rules;

        // HideCorselet declares 1,800 basis points (18%) on the torso.
        // 100 damage * 1800 / 10000 = 18 reduction, rounded down.
        var mitigated = rules.ApplyArmorMitigation(
            100, ArmorId.HideCorselet, BodyPart.Chest);

        Assert.Equal(82, mitigated);
    }

    [Fact]
    public void ApplyArmorMitigation_LeavesDamageUnchanged_ForAnUncoveredBodyPart()
    {
        var rules = PhilippineCombatPresetV8.Rules;

        // Head mitigation is zero throughout, by design section 6.1: Hukbo
        // has no helmet identity.
        var mitigated = rules.ApplyArmorMitigation(
            100, ArmorId.ImportedIron, BodyPart.Head);

        Assert.Equal(100, mitigated);
    }

    [Fact]
    public void ApplyArmorMitigation_LeavesDamageUnchanged_WhenThePresetDeclaresNoTable()
    {
        // PhilippineCombatPresetV5 predates the armor mitigation table
        // entirely and must keep behaving as though armor stops nothing,
        // since every damage-application call site in BattleSimulation calls
        // ApplyArmorMitigation unconditionally regardless of which preset a
        // battle runs under.
        var rules = PhilippineCombatPresetV5.Rules;

        Assert.False(rules.HasArmorMitigation);

        var mitigated = rules.ApplyArmorMitigation(
            100, ArmorId.LightOrganic, BodyPart.Chest);

        Assert.Equal(100, mitigated);
    }

    [Fact]
    public void ResolveArmorMitigation_Throws_WhenThePresetDeclaresNoTable()
    {
        var rules = PhilippineCombatPresetV5.Rules;

        Assert.Throws<InvalidOperationException>(
            () => rules.ResolveArmorMitigation(
                ArmorId.LightOrganic, BodyPart.Chest));
    }

    [Fact]
    public void Construction_Throws_WhenTheMitigationTableOmitsADeclaredArmorId()
    {
        var incomplete = new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>(
            FullCoverageMitigation());
        incomplete.Remove(ArmorId.ImportedIron);

        var exception = Assert.Throws<ArgumentException>(
            () => BuildMinimalRuleset(incomplete));
        Assert.Equal("armorMitigation", exception.ParamName);
    }

    [Fact]
    public void Construction_Throws_WhenAnArmorRowOmitsADeclaredBodyPart()
    {
        var mitigation = FullCoverageMitigation();
        var incompleteRow = new Dictionary<BodyPart, int>(
            mitigation[ArmorId.QuiltedCotton]);
        incompleteRow.Remove(BodyPart.Feet);
        var withIncompleteRow = new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>(
            mitigation)
        {
            [ArmorId.QuiltedCotton] = incompleteRow,
        };

        var exception = Assert.Throws<ArgumentException>(
            () => BuildMinimalRuleset(withIncompleteRow));
        Assert.Equal("armorMitigation", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Construction_Throws_WhenABasisPointValueIsOutOfRange(int outOfRangeValue)
    {
        var mitigation = FullCoverageMitigation();
        var offendingRow = new Dictionary<BodyPart, int>(
            mitigation[ArmorId.RigidCuirass])
        {
            [BodyPart.Chest] = outOfRangeValue,
        };
        var withOffendingRow = new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>(
            mitigation)
        {
            [ArmorId.RigidCuirass] = offendingRow,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildMinimalRuleset(withOffendingRow));
        Assert.Equal("armorMitigation", exception.ParamName);
    }

    [Fact]
    public void Construction_Succeeds_WhenTheMitigationTableCoversEveryArmorAndBodyPart()
    {
        var rules = BuildMinimalRuleset(FullCoverageMitigation());

        Assert.True(rules.HasArmorMitigation);
    }

    [Fact]
    public void PresetV8_IsRegisteredAndResolvesThroughTheRegistry()
    {
        Assert.True(CombatPresetRegistry.IsRegistered(
            CombatPresetId.PrecolonialPhilippinesV8));

        var rules = CombatPresetRegistry.Get(
            CombatPresetId.PrecolonialPhilippinesV8);

        Assert.Same(PhilippineCombatPresetV8.Rules, rules);
    }

    [Fact]
    public void PresetV8_RosterHasFiftyFiveEntries()
    {
        Assert.Equal(55, PhilippineCombatPresetV8.Rules.Roster.Count);
    }

    [Fact]
    public void PresetV8_RosterFieldsExactlyFiveDistinctArmors_AndNeverLightOrganic()
    {
        var distinctArmors = PhilippineCombatPresetV8.Rules.Roster
            .Select(loadout => loadout.Armor)
            .Distinct()
            .ToArray();

        Assert.Equal(5, distinctArmors.Length);
        Assert.DoesNotContain(ArmorId.LightOrganic, distinctArmors);
    }

    /// <summary>
    /// Pinned so an accidental change to any V7 target weight, weapon
    /// attribute, clash cell, rank level, roster entry, or armor mitigation
    /// value fails here rather than only in the much slower headless
    /// benchmark. Captured from an actual run of
    /// <see cref="PhilippineCombatPresetV8"/>, not invented.
    /// </summary>
    [Fact]
    public void PresetV8ContentHash_IsPinnedAndDistinctFromEveryEarlierVersion()
    {
        var v1 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var v2 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);
        var v3 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV3);
        var v4 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV4);
        var v5 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);
        var v6 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV6);
        var v7 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV7);
        var v8 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV8);

        Assert.Equal(0x79DB07403DD9308FUL, v8.ContentHash);
        Assert.NotEqual(v1.ContentHash, v8.ContentHash);
        Assert.NotEqual(v2.ContentHash, v8.ContentHash);
        Assert.NotEqual(v3.ContentHash, v8.ContentHash);
        Assert.NotEqual(v4.ContentHash, v8.ContentHash);
        Assert.NotEqual(v5.ContentHash, v8.ContentHash);
        Assert.NotEqual(v6.ContentHash, v8.ContentHash);

        // Version 7 is version 8's own parent: version 8 restates it verbatim
        // and adds the armor roster and the mitigation table. If those two
        // additions were somehow folded as nothing, this is the assertion that
        // would catch it, and it is the only pair in this list where the two
        // presets share every other table.
        Assert.NotEqual(v7.ContentHash, v8.ContentHash);
    }

    /// <summary>
    /// A full-coverage mitigation table (every declared <see cref="ArmorId"/>
    /// member, every <see cref="BodyPart"/> member, all at zero) that the
    /// out-of-range and missing-coverage tests above mutate a single entry
    /// of, so each test isolates exactly one invariant.
    /// </summary>
    private static Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>> FullCoverageMitigation() =>
        Enum.GetValues<ArmorId>().ToDictionary(
            armor => armor,
            armor => (IReadOnlyDictionary<BodyPart, int>)Enum.GetValues<BodyPart>()
                .ToDictionary(part => part, _ => 0));

    /// <summary>
    /// The smallest valid <see cref="CombatRuleset"/> that can carry an
    /// <c>armorMitigation</c> argument: one weapon, one armor, one shield,
    /// one roster entry. Mirrors the minimal-construction helpers already
    /// used in <c>CombatConfigurationTests</c>.
    /// </summary>
    private static CombatRuleset BuildMinimalRuleset(
        IReadOnlyDictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>> armorMitigation)
    {
        var uniformEntries = Enum.GetValues<BodyPart>()
            .Select(part => (part, 5))
            .ToArray();
        var general = new TargetWeightProfile(uniformEntries);
        var weaponProfile = new TargetWeightProfile(uniformEntries);
        var shieldProfile = new TargetWeightProfile(uniformEntries);

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
            ],
            armorMitigation: armorMitigation);
    }
}
