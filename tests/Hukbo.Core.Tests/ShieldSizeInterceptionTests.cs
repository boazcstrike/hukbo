using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

/// <summary>
/// Covers T1's size-aware shield interception: the formula in
/// <see cref="ClashProfile.ResolveShieldIntercept(ShieldId, WeaponId)"/>, its
/// exercise through <see cref="PhilippineCombatPresetV7"/>, and the
/// requirement that presets V1 through V6 fold none of the new data into
/// <see cref="CombatRuleset.ContentHash"/>. Every assertion below is against
/// a literal, not against the constant it is checking — a threshold read out
/// of the constant it verifies moves with it and proves nothing.
/// </summary>
public sealed class ShieldSizeInterceptionTests
{
    [Fact]
    public void ResolveShieldIntercept_MeleeAgainstTallHardwood_EqualsTheFlatV6Value()
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        var intercept = clashProfile.ResolveShieldIntercept(
            ShieldId.TallHardwood,
            WeaponId.Kampilan);

        // 2,400 is PhilippineCombatPresetV6's flat shieldIntercept value.
        // Zero shield-defeat bulk for a melee weapon must reduce the
        // size-aware formula to exactly that value.
        Assert.Equal(2_400, intercept);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Arquebus)]
    public void ResolveShieldIntercept_TallHardwoodExceedsNarrowBreastHigh_ForEveryWeapon(
        WeaponId attackerWeapon)
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        var tall = clashProfile.ResolveShieldIntercept(
            ShieldId.TallHardwood,
            attackerWeapon);
        var narrow = clashProfile.ResolveShieldIntercept(
            ShieldId.NarrowBreastHigh,
            attackerWeapon);

        Assert.True(
            tall > narrow,
            $"TallHardwood intercept {tall} must exceed NarrowBreastHigh " +
            $"intercept {narrow} for attacker {attackerWeapon}.");
    }

    [Fact]
    public void ResolveShieldIntercept_StrictlyDecreasesWithBulk_ForTallHardwood()
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        var melee = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, WeaponId.Kampilan);
        var busog = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, WeaponId.Busog);
        var bangkaw = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, WeaponId.Bangkaw);
        var arquebus = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, WeaponId.Arquebus);

        Assert.True(melee > busog, $"melee {melee} must exceed Busog {busog}.");
        Assert.True(busog > bangkaw, $"Busog {busog} must exceed Bangkaw {bangkaw}.");
        Assert.True(bangkaw > arquebus, $"Bangkaw {bangkaw} must exceed Arquebus {arquebus}.");
    }

    [Fact]
    public void ResolveShieldIntercept_StrictlyDecreasesWithBulk_ForNarrowBreastHigh()
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        var melee = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, WeaponId.Kampilan);
        var busog = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, WeaponId.Busog);
        var bangkaw = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, WeaponId.Bangkaw);
        var arquebus = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, WeaponId.Arquebus);

        Assert.True(melee > busog, $"melee {melee} must exceed Busog {busog}.");
        Assert.True(busog > bangkaw, $"Busog {busog} must exceed Bangkaw {bangkaw}.");
        Assert.True(bangkaw > arquebus, $"Bangkaw {bangkaw} must exceed Arquebus {arquebus}.");
    }

    [Theory]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Arquebus)]
    public void ResolveShieldIntercept_ProportionalLossFromBulk_IsLargerForTheNarrowerShield(
        WeaponId attackerWeapon)
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        var tallMelee = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, WeaponId.Kampilan);
        var tallBulk = clashProfile.ResolveShieldIntercept(ShieldId.TallHardwood, attackerWeapon);
        var narrowMelee = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, WeaponId.Kampilan);
        var narrowBulk = clashProfile.ResolveShieldIntercept(ShieldId.NarrowBreastHigh, attackerWeapon);

        // Proportional loss expressed as basis points out of the melee
        // baseline, scaled by 10,000 and compared as integers so no
        // floating-point value enters the assertion.
        var tallLossBasisPoints = (long)(tallMelee - tallBulk) * 10_000 / tallMelee;
        var narrowLossBasisPoints = (long)(narrowMelee - narrowBulk) * 10_000 / narrowMelee;

        Assert.True(
            narrowLossBasisPoints > tallLossBasisPoints,
            $"NarrowBreastHigh's proportional loss {narrowLossBasisPoints} " +
            $"must exceed TallHardwood's {tallLossBasisPoints} for attacker " +
            $"{attackerWeapon}.");
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Arquebus)]
    public void ResolveShieldIntercept_NoneShield_IsAlwaysZero(WeaponId attackerWeapon)
    {
        var clashProfile = PhilippineCombatPresetV7.Rules.ClashProfile;

        Assert.Equal(0, clashProfile.ResolveShieldIntercept(ShieldId.None, attackerWeapon));
    }

    [Fact]
    public void PhilippinePresetV1_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0x59FB4CA563D87A49UL, PhilippineCombatPreset.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV2_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0x10AB1CC226AB3636UL, PhilippineCombatPresetV2.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV3_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0xCD790E489293B304UL, PhilippineCombatPresetV3.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV4_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0x4E3E4F8C0A3822E0UL, PhilippineCombatPresetV4.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV5_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0x55F4F5B36EE59CF7UL, PhilippineCombatPresetV5.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV6_ContentHash_IsUnmovedByTheSizeAwareShieldWork()
    {
        Assert.Equal(0xCF8505296849E9ACUL, PhilippineCombatPresetV6.Rules.ContentHash);
    }

    // T1 rebuild note: V7 was originally built forward from V6 by mistake,
    // which fields no ranged weapon and no ShieldId.TallHardwood roster row,
    // making the shield-size-versus-projectile-size feature this preset
    // exists for structurally unobservable. V7 is rebuilt on
    // PhilippineCombatPresetV5 instead, so its content hash is a new value
    // rather than a restatement of the mistaken draft's. Read from a real
    // run per CLAUDE.md section 6, never hand-calculated.
    [Fact]
    public void PhilippinePresetV7_ContentHash_IsRebuiltOnV5NotV6()
    {
        Assert.Equal(0x9FE22357E6129403UL, PhilippineCombatPresetV7.Rules.ContentHash);
    }

    [Fact]
    public void PhilippinePresetV7_Roster_HasElevenRows()
    {
        Assert.Equal(11, PhilippineCombatPresetV7.Rules.Roster.Count);
    }

    [Theory]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void PhilippinePresetV7_Roster_ContainsEveryRangedWeapon(WeaponId weapon)
    {
        Assert.Contains(
            PhilippineCombatPresetV7.Rules.Roster,
            entry => entry.Weapon == weapon && entry.Shield == ShieldId.None);
    }

    [Theory]
    [InlineData(ShieldId.TallHardwood)]
    [InlineData(ShieldId.NarrowBreastHigh)]
    public void PhilippinePresetV7_Roster_ContainsBothShieldSizesOnKalisAndItak(ShieldId shield)
    {
        Assert.Contains(
            PhilippineCombatPresetV7.Rules.Roster,
            entry => entry.Weapon == WeaponId.Kalis && entry.Shield == shield);
        Assert.Contains(
            PhilippineCombatPresetV7.Rules.Roster,
            entry => entry.Weapon == WeaponId.Itak && entry.Shield == shield);
    }
}
