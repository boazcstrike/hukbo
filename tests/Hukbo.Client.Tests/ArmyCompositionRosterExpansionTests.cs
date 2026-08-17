using System.Linq;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// RU-43: the army-composition rank sliders were inert while combat preset
/// V5 was active because <c>ArenaGame.BuildScenario</c> left
/// <c>Scenario.RosterCounts</c> unset rather than risk a length mismatch
/// against V5's longer roster. These tests cover
/// <see cref="ArenaGame.ExpandCompositionToRosterCounts"/>, the static pure
/// function that fixes it by spreading each of the four rank sliders across
/// every roster row that carries that rank. Pure per <c>hukbo-client-ui</c>:
/// no <c>ArenaGame</c> instance, no <c>SpriteBatch</c>, no window.
/// </summary>
public sealed class ArmyCompositionRosterExpansionTests
{
    private static readonly ArmyComposition DefaultComposition =
        ArmyComposition.Default;

    [Fact]
    public void V4RosterCountsPassThroughUnchangedOneRankOneRow()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV4)
            .Roster;

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            DefaultComposition);

        // V4 fields exactly one roster row per rank (Kampilan/Datu,
        // Wasay/Maharlika, Kalis/Timawa, Itak/AlipingNamamahay), so each
        // rank's slider count passes straight through unchanged — the
        // behavior the panel already had before RU-43.
        Assert.Equal(roster.Count, result.Length);
        Assert.Equal(DefaultComposition.CategoryCounts, result.AsSpan().ToArray());
        Assert.Equal(DefaultComposition.UnitsPerTeam, result.Sum());
    }

    [Fact]
    public void V5RosterCountsMatchRosterLengthAndSumToUnitsPerTeam()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV5)
            .Roster;

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            DefaultComposition);

        // Scenario.Validate requires exactly this length and exactly this
        // sum whenever RosterCounts is set at all — a mismatch here is a
        // battle that refuses to start.
        Assert.Equal(roster.Count, result.Length);
        Assert.Equal(DefaultComposition.UnitsPerTeam, result.Sum());
    }

    [Fact]
    public void V5DefaultCompositionMatchesRU24CalibratedProportions()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV5)
            .Roster;

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            DefaultComposition);

        // Roster order: Kampilan(Datu), Wasay(Maharlika), Kalis(Timawa),
        // Itak(AlipingNamamahay), Bangkaw(Timawa), Busog(Timawa),
        // Arquebus(Timawa), Kalis+Shield(Timawa), Itak+Shield(AlipingNamamahay).
        // Expected values are the RU-24/RU-45 calibrated share weights
        // [19, 19, 10, 9, 11, 8, 6, 6, 6] apportioned by largest remainder
        // against each rank's slider count at the 2026-08-14 calibrated
        // default composition (Datu 48, Maharlika 47, Timawa 110,
        // AlipingNamamahay 45). The shield size against projectile size
        // package dropped the Kalis+Shield and Itak+Shield weights from 9
        // to 6 apiece, seeding the two narrow-breast-high-shield rows combat
        // preset V7 adds; this shared weight table apportions V5's roster
        // too, so V5's tall-hardwood-shield rows move down with it even
        // though V5 fields no narrow-shield row of its own.
        int[] expected = [48, 47, 27, 27, 30, 21, 16, 16, 18];

        Assert.Equal(expected, result.AsSpan().ToArray());
    }

    [Fact]
    public void TimawaSplitUsesCalibratedWeightsNotAnEvenSplitAcrossItsFiveRows()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV5)
            .Roster;

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            DefaultComposition);

        // Timawa's five roster rows (indices 2, 4, 5, 6, 7 — Kalis, Bangkaw,
        // Busog, Arquebus, Kalis+Shield) sum to the rank's 110-unit slider
        // count no matter how the split is computed, so the sum alone
        // cannot catch a regression that flattens the split to an even one.
        // An even split of 110 across 5 rows is {22, 22, 22, 22, 22}. The
        // calibrated weights RU-24/RU-45 measured, as the shield size
        // against projectile size package left them (Kalis+Shield's weight
        // dropped from 9 to 6, tying it with Arquebus), do not produce that:
        // Bangkaw (weight 11, the highest of the five) gets 30, and the two
        // weight-6 rows, Arquebus and Kalis+Shield, each get 16 — the tied
        // low value an even split could never produce alongside a distinct
        // high one. If the calibrated-weight mapping is deleted or replaced
        // with an even split, these assertions turn red.
        int[] timawaCounts =
            [result[2], result[4], result[5], result[6], result[7]];

        Assert.Equal(110, timawaCounts.Sum());
        Assert.NotEqual(new[] { 22, 22, 22, 22, 22 }, timawaCounts);
        Assert.Contains(30, timawaCounts);
        Assert.Equal(2, timawaCounts.Count(count => count == 16));
    }

    [Fact]
    public void UnmappedRankThrowsRatherThanSilentlyDroppingWarriors()
    {
        CombatLoadout[] roster =
        [
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, RankId.Ayuey),
        ];

        Assert.Throws<InvalidOperationException>(
            () => ArenaGame.ExpandCompositionToRosterCounts(
                roster,
                DefaultComposition));
    }
}
