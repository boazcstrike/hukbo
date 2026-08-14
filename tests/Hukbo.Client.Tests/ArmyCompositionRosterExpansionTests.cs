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
        // [19, 19, 10, 9, 11, 8, 6, 9, 9] apportioned by largest remainder
        // against each rank's slider count at the 2026-08-14 calibrated
        // default composition (Datu 48, Maharlika 47, Timawa 110,
        // AlipingNamamahay 45).
        int[] expected = [48, 47, 25, 23, 28, 20, 15, 22, 22];

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
        // An even split of 110 across 5 rows is {22, 22, 22, 22, 22} — it
        // contains neither a 15 nor a 28. The calibrated weights RU-24/RU-45
        // measured do produce both: Arquebus (weight 6, the lowest of the
        // five) gets 15, and Bangkaw (weight 11, the highest) gets 28. If the
        // calibrated-weight mapping is deleted or replaced with an even
        // split, these two assertions turn red.
        int[] timawaCounts =
            [result[2], result[4], result[5], result[6], result[7]];

        Assert.Equal(110, timawaCounts.Sum());
        Assert.Contains(15, timawaCounts);
        Assert.Contains(28, timawaCounts);
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
