using System.Linq;
using System.Reflection;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// The armor identity package's client-owned coverage: the
/// <see cref="ArmorComposition"/> record itself, and
/// <see cref="ArenaGame.ExpandCompositionToRosterCounts"/>'s three-argument
/// overload, which spends it against combat preset V7's forty-five-row
/// roster (nine base loadouts, each fielded in five armor-baseline
/// variants). Pure per <c>hukbo-client-ui</c>: no <c>ArenaGame</c> instance,
/// no <c>SpriteBatch</c>, no window.
/// <para>
/// Every default percentage this test file exercises through
/// <see cref="ArmorComposition.Default"/> is a PROVISIONAL gameplay-tuning
/// choice under CLAUDE.md section 7 - see that type's own remarks and
/// docs/research/HISTORICAL_1500s_ARMOR.md question 11. No assertion below
/// should be read as confirming a historical proportion of armored
/// fighters; each one only confirms that the record's own arithmetic and
/// the roster-splitting mechanics built on top of it behave as designed.
/// </para>
/// </summary>
public sealed class ArmorCompositionTests
{
    private static readonly IReadOnlyList<CombatLoadout> V8Roster =
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV8).Roster;

    [Fact]
    public void DefaultSumsToExactlyOneHundred()
    {
        var total = ArmorComposition.Default.CategoryCounts.Sum();

        Assert.Equal(100, total);
    }

    [Fact]
    public void DefaultIsValid()
    {
        Assert.True(ArmorComposition.Default.IsValid());
    }

    [Fact]
    public void IsValidRejectsPercentagesThatDoNotSumToOneHundred()
    {
        // 35 + 30 + 20 + 12 + 2 = 99, one short of the required 100.
        var composition = new ArmorComposition(35, 30, 20, 12, 2);

        Assert.False(composition.IsValid());
    }

    [Fact]
    public void IsValidRejectsANegativeCategoryCount()
    {
        // -5 + 35 + 30 + 20 + 20 = 100, so the sum alone would pass; the
        // negative category is what must fail this check on its own.
        var composition = new ArmorComposition(-5, 35, 30, 20, 20);

        Assert.False(composition.IsValid());
    }

    [Theory]
    [InlineData(35, 30, 20, 12, 3)]
    [InlineData(100, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 100)]
    [InlineData(20, 20, 20, 20, 20)]
    public void ExpandedRosterCountsAreDeterministicAcrossRepeatedCalls(
        int nonePercent,
        int quiltedCottonPercent,
        int hideCorseletPercent,
        int rigidCuirassPercent,
        int importedIronPercent)
    {
        var armorComposition = new ArmorComposition(
            nonePercent,
            quiltedCottonPercent,
            hideCorseletPercent,
            rigidCuirassPercent,
            importedIronPercent);

        var first = ArenaGame.ExpandCompositionToRosterCounts(
            V8Roster,
            ArmyComposition.Default,
            armorComposition);
        var second = ArenaGame.ExpandCompositionToRosterCounts(
            V8Roster,
            ArmyComposition.Default,
            armorComposition);

        // Same inputs must always produce the same roster counts: no
        // per-call state, no hidden randomness, nothing that could make two
        // separate expansions of the same composition diverge.
        // ImmutableArray<int> implements IEquatable<ImmutableArray<int>> by
        // comparing the underlying array reference, not its contents, so
        // Assert.Equal on the arrays directly would fail even for two
        // element-wise-identical results; comparing .ToArray() forces the
        // element-wise comparison this test actually means.
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    /// <summary>
    /// Both factions field the identical armor share not because the
    /// client copies one faction's counts onto the other, but because
    /// <see cref="Scenario.RosterCounts"/> is a single array shared by the
    /// whole scenario rather than one array per faction. This test proves
    /// that structural fact by reflection, so a future change that quietly
    /// grows a second, faction-specific roster-count property is caught
    /// here rather than only showing up as an unfair draw in a live battle.
    /// </summary>
    [Fact]
    public void ScenarioRosterCountsIsOneArraySharedByBothFactions()
    {
        var rosterCountProperties = typeof(Scenario)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.Contains(
                "RosterCount",
                StringComparison.Ordinal))
            .ToList();

        Assert.Single(rosterCountProperties);
        Assert.Equal("RosterCounts", rosterCountProperties[0].Name);
    }

    [Theory]
    [InlineData(35, 30, 20, 12, 3)]
    [InlineData(100, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 100)]
    [InlineData(20, 20, 20, 20, 20)]
    public void EveryExpandedRosterIndexIsInRangeForTheFiftyFiveRowV8Roster(
        int nonePercent,
        int quiltedCottonPercent,
        int hideCorseletPercent,
        int rigidCuirassPercent,
        int importedIronPercent)
    {
        var armorComposition = new ArmorComposition(
            nonePercent,
            quiltedCottonPercent,
            hideCorseletPercent,
            rigidCuirassPercent,
            importedIronPercent);

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            V8Roster,
            ArmyComposition.Default,
            armorComposition);

        // Nine base loadouts times five armor baselines each.
        Assert.Equal(55, V8Roster.Count);
        Assert.Equal(V8Roster.Count, result.Length);

        foreach (var count in result)
        {
            Assert.True(count >= 0);
        }

        Assert.Equal(ArmyComposition.Default.UnitsPerTeam, result.Sum());
    }

    [Fact]
    public void ArmorSplitWithinABaseLoadoutFollowsTheConfiguredPercentagesNotAnEvenSplit()
    {
        // An extreme composition - every unit fields ImportedIron - makes
        // the expected shape unambiguous: every one of V7's forty-five rows
        // whose armor baseline is not ImportedIron must receive zero
        // warriors, and the five ImportedIron rows must together carry the
        // whole roster.
        var allImportedIron = new ArmorComposition(0, 0, 0, 0, 100);

        var result = ArenaGame.ExpandCompositionToRosterCounts(
            V8Roster,
            ArmyComposition.Default,
            allImportedIron);

        for (var index = 0; index < V8Roster.Count; index++)
        {
            if (V8Roster[index].Armor != ArmorId.ImportedIron)
            {
                Assert.Equal(0, result[index]);
            }
        }

        var importedIronTotal = Enumerable.Range(0, V8Roster.Count)
            .Where(index => V8Roster[index].Armor == ArmorId.ImportedIron)
            .Sum(index => result[index]);

        Assert.Equal(ArmyComposition.Default.UnitsPerTeam, importedIronTotal);
    }
}
