using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 22 of docs/plans/2026-08-07-sandata-scaffold.md: the 38-row firearm
/// catalog, the two name sets, and <see cref="FirearmRuleset.ContentHash"/>.
/// </summary>
public sealed class FirearmCatalogTests
{
    /// <summary>
    /// Design section 9's five distinct fire-mode sets. No other combination
    /// may appear on any row.
    /// </summary>
    private static readonly FireModeSet[] DistinctModeSets =
    {
        FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
        FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst3,
        FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst2 | FireModeSet.Auto,
        FireModeSet.Single,
        FireModeSet.Safe | FireModeSet.Single,
    };

    [Fact]
    public void Rows_HasExactlyThirtyEightEntries()
    {
        Assert.Equal(38, FirearmCatalog.Rows.Count);
    }

    [Fact]
    public void Rows_AreInDenseFirearmIdOrder()
    {
        for (var i = 0; i < FirearmCatalog.Rows.Count; i++)
        {
            Assert.Equal(i, (int)FirearmCatalog.Rows[i].Id);
        }
    }

    [Theory]
    [MemberData(nameof(AllRowIndexes))]
    public void EveryRow_ModesIsOneOfTheFiveDistinctSets(int index)
    {
        var modes = FirearmCatalog.Rows[index].Modes;

        Assert.Contains(modes, DistinctModeSets);
    }

    [Theory]
    [MemberData(nameof(AllRowIndexes))]
    public void NoRow_CarriesBothBurst2AndBurst3(int index)
    {
        var modes = FirearmCatalog.Rows[index].Modes;

        var hasBoth = modes.HasFlag(FireModeSet.Burst2) && modes.HasFlag(FireModeSet.Burst3);

        Assert.False(hasBoth, $"Row {FirearmCatalog.Rows[index].Id} carries both Burst2 and Burst3.");
    }

    [Fact]
    public void BothNameSets_HaveAnEntryForEveryRow()
    {
        Assert.Equal(FirearmCatalog.Rows.Count, WeaponNameSets.Manufacturer.Count);
        Assert.Equal(FirearmCatalog.Rows.Count, WeaponNameSets.Generic.Count);

        foreach (var row in FirearmCatalog.Rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(WeaponNameSets.GetName(row.Id, WeaponNameSetId.Manufacturer)));
            Assert.False(string.IsNullOrWhiteSpace(WeaponNameSets.GetName(row.Id, WeaponNameSetId.Generic)));
        }
    }

    [Fact]
    public void M4AndM4A1_DifferInModes()
    {
        var m4 = FirearmCatalog.Rows[(int)FirearmId.M4].Modes;
        var m4A1 = FirearmCatalog.Rows[(int)FirearmId.M4A1].Modes;

        Assert.NotEqual(m4, m4A1);
        Assert.True(m4.HasFlag(FireModeSet.Burst3));
        Assert.False(m4.HasFlag(FireModeSet.Auto));
        Assert.True(m4A1.HasFlag(FireModeSet.Auto));
        Assert.False(m4A1.HasFlag(FireModeSet.Burst3));
    }

    [Fact]
    public void Ak12TwentyEighteenTwentyOne_HasBurst2_And2023DoesNot()
    {
        var ak12 = FirearmCatalog.Rows[(int)FirearmId.Ak12].Modes;
        var ak122023 = FirearmCatalog.Rows[(int)FirearmId.Ak122023].Modes;

        Assert.True(ak12.HasFlag(FireModeSet.Burst2));
        Assert.False(ak122023.HasFlag(FireModeSet.Burst2));
    }

    [Fact]
    public void AugA3_MechanismGroupIsBullpup()
    {
        var aug = FirearmCatalog.Rows[(int)FirearmId.AugA3];

        Assert.Equal(MechanismGroup.Bullpup, aug.Mechanism);
    }

    /// <summary>
    /// Recorded from the built code, never calculated by hand — the same rule
    /// <c>SandataRulesetTests.ModernTacticalV1_ContentHashIsPinned</c> follows.
    /// A future change to <see cref="FirearmDefinition"/>'s field list, field
    /// order, the catalog's row data, or the FNV-1a fold moves this value,
    /// which is a new preset version with a new recorded expectation, not a
    /// fix to this test.
    /// </summary>
    [Fact]
    public void ModernTacticalV1_ContentHashIsPinned()
    {
        Assert.Equal(13_098_676_811_469_352_013UL, FirearmRuleset.ModernTacticalV1.ContentHash);
    }

    /// <summary>
    /// Changing exactly one field of exactly one row must move the fold.
    /// Covers every field <see cref="FirearmDefinition"/> declares, on the
    /// first row, matching the one-field-at-a-time shape
    /// <c>SandataRulesetTests.ChangingAnySingleField_MovesTheContentHash</c>
    /// already establishes for <c>SandataRuleset</c>.
    /// </summary>
    [Theory]
    [InlineData(FirearmField.Id)]
    [InlineData(FirearmField.Class)]
    [InlineData(FirearmField.Caliber)]
    [InlineData(FirearmField.Mechanism)]
    [InlineData(FirearmField.Modes)]
    [InlineData(FirearmField.ReadyMs)]
    [InlineData(FirearmField.AimBaseMs)]
    [InlineData(FirearmField.AimPerBamMs)]
    [InlineData(FirearmField.ResetMs)]
    [InlineData(FirearmField.TurnBamPerTick)]
    [InlineData(FirearmField.AutoBandMaxWu)]
    [InlineData(FirearmField.BurstBandMaxWu)]
    [InlineData(FirearmField.SingleBandMaxWu)]
    [InlineData(FirearmField.DispersionAtZeroWu)]
    [InlineData(FirearmField.DispersionAtMaxWu)]
    [InlineData(FirearmField.MaxEffectiveWu)]
    [InlineData(FirearmField.MagazineCapacity)]
    [InlineData(FirearmField.ReloadMs)]
    [InlineData(FirearmField.CyclicRpm)]
    [InlineData(FirearmField.ExemptFromLoweredRule)]
    public void ChangingAnySingleFieldOnAnyRow_MovesTheContentHash(FirearmField field)
    {
        var baselineRows = FirearmCatalog.Rows;
        var baselineHash = new FirearmRuleset(baselineRows).ContentHash;

        var changedRows = baselineRows.ToArray();
        changedRows[0] = WithOneFieldChanged(changedRows[0], field);
        var changedHash = new FirearmRuleset(changedRows).ContentHash;

        Assert.NotEqual(baselineHash, changedHash);
    }

    /// <summary>
    /// Every field <see cref="FirearmDefinition"/> declares, so the theory
    /// above and this switch stay in sync by construction.
    /// </summary>
    public enum FirearmField
    {
        Id,
        Class,
        Caliber,
        Mechanism,
        Modes,
        ReadyMs,
        AimBaseMs,
        AimPerBamMs,
        ResetMs,
        TurnBamPerTick,
        AutoBandMaxWu,
        BurstBandMaxWu,
        SingleBandMaxWu,
        DispersionAtZeroWu,
        DispersionAtMaxWu,
        MaxEffectiveWu,
        MagazineCapacity,
        ReloadMs,
        CyclicRpm,
        ExemptFromLoweredRule,
    }

    public static IEnumerable<object[]> AllRowIndexes()
    {
        for (var i = 0; i < FirearmCatalog.Rows.Count; i++)
        {
            yield return new object[] { i };
        }
    }

    private static FirearmDefinition WithOneFieldChanged(
        FirearmDefinition baseline, FirearmField field) => field switch
        {
            FirearmField.Id => baseline with { Id = baseline.Id == FirearmId.Ak47 ? FirearmId.Akm : FirearmId.Ak47 },
            FirearmField.Class => baseline with
            {
                Class = baseline.Class == WeaponClass.Rifle ? WeaponClass.Pistol : WeaponClass.Rifle,
            },
            FirearmField.Caliber => baseline with
            {
                Caliber = baseline.Caliber == CaliberFamily.Cal762X39
                    ? CaliberFamily.Cal556X45
                    : CaliberFamily.Cal762X39,
            },
            FirearmField.Mechanism => baseline with
            {
                Mechanism = baseline.Mechanism == MechanismGroup.Ak ? MechanismGroup.Ar : MechanismGroup.Ak,
            },
            FirearmField.Modes => baseline with { Modes = baseline.Modes ^ FireModeSet.Safe },
            FirearmField.ReadyMs => baseline with { ReadyMs = baseline.ReadyMs + 1 },
            FirearmField.AimBaseMs => baseline with { AimBaseMs = baseline.AimBaseMs + 1 },
            FirearmField.AimPerBamMs => baseline with { AimPerBamMs = baseline.AimPerBamMs + 1 },
            FirearmField.ResetMs => baseline with { ResetMs = baseline.ResetMs + 1 },
            FirearmField.TurnBamPerTick => baseline with { TurnBamPerTick = baseline.TurnBamPerTick + 1 },
            FirearmField.AutoBandMaxWu => baseline with { AutoBandMaxWu = baseline.AutoBandMaxWu + 1 },
            FirearmField.BurstBandMaxWu => baseline with { BurstBandMaxWu = baseline.BurstBandMaxWu + 1 },
            FirearmField.SingleBandMaxWu => baseline with { SingleBandMaxWu = baseline.SingleBandMaxWu + 1 },
            FirearmField.DispersionAtZeroWu => baseline with { DispersionAtZeroWu = baseline.DispersionAtZeroWu + 1 },
            FirearmField.DispersionAtMaxWu => baseline with { DispersionAtMaxWu = baseline.DispersionAtMaxWu + 1 },
            FirearmField.MaxEffectiveWu => baseline with { MaxEffectiveWu = baseline.MaxEffectiveWu + 1 },
            FirearmField.MagazineCapacity => baseline with { MagazineCapacity = baseline.MagazineCapacity + 1 },
            FirearmField.ReloadMs => baseline with { ReloadMs = baseline.ReloadMs + 1 },
            FirearmField.CyclicRpm => baseline with { CyclicRpm = baseline.CyclicRpm + 1 },
            FirearmField.ExemptFromLoweredRule => baseline with
            {
                ExemptFromLoweredRule = !baseline.ExemptFromLoweredRule,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unhandled firearm field."),
        };
}
