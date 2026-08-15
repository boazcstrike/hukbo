using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

/// <summary>
/// Pins <see cref="CombatPresetId.PrecolonialPhilippinesV6"/>, the cadence
/// retune that answers the CL-1, CL-3, and CL-7 legibility failures recorded in
/// docs/development/smoke-checklist.md on 2026-08-11.
/// </summary>
/// <remarks>
/// Four things are asserted, and the fourth is the one the design rests on.
/// The identifier and the six retuned values are pinned literally; every table
/// V6 was supposed to leave alone is compared against V4 rather than restated,
/// so an accidental rewrite of the roster, the ranks, the target weights, the
/// shield multipliers, or the clash profile fails here rather than surfacing as
/// a moved hash nobody can explain. The last test asserts that damage per tick
/// stayed within two per cent of V4's, in integer arithmetic, which is what
/// makes this a cadence change rather than a balance change.
/// <para>
/// Every value below is a PROVISIONAL gameplay tuning value under CLAUDE.md
/// section 7, never a historical measurement.
/// </para>
/// </remarks>
public sealed class CombatCadenceV6Tests
{
    private const int MaximumDriftPercent = 2;

    private static CombatRuleset V4 =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV4);

    private static CombatRuleset V6 =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV6);

    private static readonly WeaponId[] Weapons =
    [
        WeaponId.Kampilan,
        WeaponId.Wasay,
        WeaponId.Kalis,
        WeaponId.Itak,
    ];

    [Fact]
    public void V6_IsAppendedAtSixAndIsRegistered()
    {
        // The numeric value is the hashed quantity. Renumbering it silently
        // invalidates every replay recorded against V6, exactly as it would
        // for any earlier preset.
        Assert.Equal(6, (int)CombatPresetId.PrecolonialPhilippinesV6);
        Assert.True(
            CombatPresetRegistry.IsRegistered(
                CombatPresetId.PrecolonialPhilippinesV6));
        Assert.Equal(CombatPresetId.PrecolonialPhilippinesV6, V6.Id);
    }

    [Theory]
    // weapon, shield, damage, cooldown ticks, combo cooldown ticks
    [InlineData(WeaponId.Kampilan, ShieldId.None, 26, 12, 7)]
    [InlineData(WeaponId.Wasay, ShieldId.None, 32, 14, 9)]
    [InlineData(WeaponId.Kalis, ShieldId.None, 22, 10, 6)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, 20, 10, 6)]
    [InlineData(WeaponId.Itak, ShieldId.None, 20, 9, 5)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, 18, 9, 5)]
    public void V6_PinsTheRetunedCadence(
        WeaponId weapon,
        ShieldId shield,
        int expectedDamage,
        int expectedCooldownTicks,
        int expectedComboCooldownTicks)
    {
        var profile = V6.ResolveWeaponProfile(weapon, shield);

        Assert.Equal(expectedDamage, profile.DamagePerAttack);
        Assert.Equal(expectedCooldownTicks, profile.AttackCooldownTicks);
        Assert.Equal(expectedComboCooldownTicks, profile.ComboCooldownTicks);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None)]
    [InlineData(WeaponId.Wasay, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Itak, ShieldId.None)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood)]
    public void V6_ChangesCadenceAndDamageOnly_NotReachOrComboChances(
        WeaponId weapon,
        ShieldId shield)
    {
        var v4 = V4.ResolveWeaponProfile(weapon, shield);
        var v6 = V6.ResolveWeaponProfile(weapon, shield);

        Assert.Equal(v4.AttackRangeRaw, v6.AttackRangeRaw);
        Assert.Equal(
            v4.ComboOpenChanceBasisPoints,
            v6.ComboOpenChanceBasisPoints);
        Assert.Equal(
            v4.ComboContinueChanceBasisPoints,
            v6.ComboContinueChanceBasisPoints);
        Assert.Equal(v4.ComboMaxSteps, v6.ComboMaxSteps);

        // Positive control: this suite would be worthless if V6 turned out to
        // be a copy of V4, so assert the cadence actually moved.
        Assert.True(v6.AttackCooldownTicks > v4.AttackCooldownTicks);
        Assert.True(v6.DamagePerAttack > v4.DamagePerAttack);
    }

    [Fact]
    public void V6_RestatesEveryNonCadenceTableFromV4()
    {
        var v4 = V4;
        var v6 = V6;

        Assert.Equal(v4.Version, v6.Version);
        Assert.Equal(v4.Roster, v6.Roster);

        foreach (var rank in Enum.GetValues<RankId>())
        {
            Assert.Equal(v4.ResolveLevel(rank), v6.ResolveLevel(rank));
        }

        foreach (var bodyPart in Enum.GetValues<BodyPart>())
        {
            Assert.Equal(
                v4.GeneralTargets.Get(bodyPart),
                v6.GeneralTargets.Get(bodyPart));

            foreach (var weapon in Weapons)
            {
                Assert.Equal(
                    v4.ResolveWeaponWeight(weapon, bodyPart),
                    v6.ResolveWeaponWeight(weapon, bodyPart));
            }

            // Scoped to the shields these two frozen presets actually declare
            // rather than the bare ShieldId enum, for the same reason the
            // weapon sweep above is scoped to Weapons: V4 and V6 are frozen
            // and never gain a shield ShieldId adds later, so a full-enum
            // sweep would throw on a shield neither preset was asked to know
            // about. ShieldId.NarrowBreastHigh, appended for the shield-size
            // package, is the member that first made this bite.
            foreach (var shield in v4.Roster
                .Select(loadout => loadout.Shield)
                .Distinct()
                .OrderBy(id => (int)id))
            {
                Assert.Equal(
                    v4.ResolveDefenseMultiplier(shield, bodyPart),
                    v6.ResolveDefenseMultiplier(shield, bodyPart));
            }
        }

        // The preset identifier folds into the content hash, so these two can
        // never be equal even though every table above is. Asserting it keeps
        // a future "restate V4 exactly" edit from quietly producing a preset
        // that is V4 under a second name.
        Assert.NotEqual(v4.ContentHash, v6.ContentHash);
    }

    [Fact]
    public void V6_RestatesTheClashProfileFromV4()
    {
        var v4 = V4.ClashProfile;
        var v6 = V6.ClashProfile;

        Assert.Equal(
            v4.ShieldInterceptBasisPoints,
            v6.ShieldInterceptBasisPoints);
        Assert.Equal(
            v4.MinimumHardShareBasisPoints,
            v6.MinimumHardShareBasisPoints);
        Assert.Equal(
            v4.MaximumHardShareBasisPoints,
            v6.MaximumHardShareBasisPoints);
        Assert.Equal(
            v4.MaximumInterceptionBasisPoints,
            v6.MaximumInterceptionBasisPoints);

        foreach (var defender in Weapons)
        {
            Assert.Equal(
                v4.ResolveVoid(defender, ShieldId.None),
                v6.ResolveVoid(defender, ShieldId.None));
            Assert.Equal(
                v4.ResolveHardShareBase(defender),
                v6.ResolveHardShareBase(defender));
            Assert.Equal(
                v4.ResolveHardShareMultiplier(defender),
                v6.ResolveHardShareMultiplier(defender));

            foreach (var attacker in Weapons)
            {
                Assert.Equal(
                    v4.ResolveWeaponIntercept(defender, ShieldId.None, attacker),
                    v6.ResolveWeaponIntercept(defender, ShieldId.None, attacker));
            }
        }
    }

    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None)]
    [InlineData(WeaponId.Wasay, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Itak, ShieldId.None)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood)]
    public void V6_HoldsDamagePerTickWithinTwoPercentOfV4(
        WeaponId weapon,
        ShieldId shield)
    {
        // This is the invariant the whole design rests on. Time to kill is
        // unchanged because damage per tick is unchanged; the legibility gain
        // comes entirely from halving how many blows deliver it. An edit that
        // moves a damage value without moving its cooldown breaks the design
        // rather than merely retuning it, and it breaks here.
        //
        // Compared by cross-multiplication so the assertion is exact integer
        // arithmetic. Floating point has no place in a determinism test even
        // when it only reads the preset.
        var v4 = V4.ResolveWeaponProfile(weapon, shield);
        var v6 = V6.ResolveWeaponProfile(weapon, shield);

        var difference = Math.Abs(
            (v6.DamagePerAttack * v4.AttackCooldownTicks) -
            (v4.DamagePerAttack * v6.AttackCooldownTicks));

        var allowed =
            MaximumDriftPercent * v4.DamagePerAttack * v6.AttackCooldownTicks;

        Assert.True(
            difference * 100 <= allowed,
            $"{weapon} ({shield}) drifted more than {MaximumDriftPercent}% in " +
            $"damage per tick: V4 is {v4.DamagePerAttack}/" +
            $"{v4.AttackCooldownTicks}, V6 is {v6.DamagePerAttack}/" +
            $"{v6.AttackCooldownTicks}.");
    }

    [Fact]
    public void V4AndV5_AreUnmodifiedByTheV6Retune()
    {
        // V6 exists as a new preset precisely so V4's and V5's replays keep
        // reproducing. Spot-check the two cadence values a careless in-place
        // retune would have moved first.
        var v4Itak = V4.ResolveWeaponProfile(WeaponId.Itak, ShieldId.None);
        Assert.Equal(9, v4Itak.DamagePerAttack);
        Assert.Equal(4, v4Itak.AttackCooldownTicks);

        var v5 = CombatPresetRegistry.Get(
            CombatPresetId.PrecolonialPhilippinesV5);
        var v5Kampilan =
            v5.ResolveWeaponProfile(WeaponId.Kampilan, ShieldId.None);
        Assert.Equal(15, v5Kampilan.DamagePerAttack);
        Assert.Equal(7, v5Kampilan.AttackCooldownTicks);
    }
}
