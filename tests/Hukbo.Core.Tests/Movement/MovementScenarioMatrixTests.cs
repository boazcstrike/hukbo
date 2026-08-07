using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Self-tests for <see cref="MovementScenarioMatrix"/>. These assert the
/// combinatorial invariants of design section 17 — counts, uniqueness,
/// canonical enumeration order, and mirror counts — without ever
/// constructing a simulation or running a battle. The matchup runs belong
/// to the five weapon sessions.
/// </summary>
public sealed class MovementScenarioMatrixTests
{
    private const int LoadoutCount = 6;
    private const int PairCount = 21;
    private const int MatchupCount = 231;

    [Fact]
    public void CanonicalLoadouts_ListTheSixDesignLoadoutsInCanonicalOrder()
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;

        Assert.Equal(LoadoutCount, loadouts.Length);
        Assert.Equal(
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            loadouts[0]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            loadouts[1]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
            loadouts[2]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
            loadouts[3]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
            loadouts[4]);
        Assert.Equal(
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
            loadouts[5]);
    }

    [Fact]
    public void CanonicalLoadouts_AllCarryTheDefaultRank()
    {
        Assert.All(
            MovementScenarioMatrix.CanonicalLoadouts,
            loadout => Assert.Equal(RankId.Timawa, loadout.Rank));
    }

    [Fact]
    public void CanonicalLoadoutCodes_MatchTheDesignAbbreviationsInOrder()
    {
        Assert.Equal(
            new[] { "KP", "WA", "KA", "IT", "KS", "IS" },
            MovementScenarioMatrix.CanonicalLoadoutCodes);
    }

    [Fact]
    public void OneVersusOnePairs_CountExactlyTwentyOne()
    {
        Assert.Equal(PairCount, MovementScenarioMatrix.EnumerateOneVersusOnePairs().Length);
    }

    [Fact]
    public void OneVersusOnePairs_AreUnique()
    {
        var pairs = MovementScenarioMatrix.EnumerateOneVersusOnePairs();

        Assert.Equal(pairs.Length, pairs.Distinct().Count());
    }

    [Fact]
    public void OneVersusOnePairs_FollowTheCanonicalNestedEnumerationOrder()
    {
        // Independent oracle: the nested i <= j enumeration written out
        // inline, so the production enumerator is compared against an
        // obviously correct sequence rather than against itself.
        var expected = new List<(int First, int Second)>();
        for (var i = 0; i < LoadoutCount; i++)
        {
            for (var j = i; j < LoadoutCount; j++)
            {
                expected.Add((i, j));
            }
        }

        var actual = MovementScenarioMatrix
            .EnumerateOneVersusOnePairs()
            .Select(pair => (pair.FirstLoadoutIndex, pair.SecondLoadoutIndex))
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OneVersusOnePairs_ContainExactlySixMirrors()
    {
        var mirrors = MovementScenarioMatrix
            .EnumerateOneVersusOnePairs()
            .Where(pair => pair.IsMirror)
            .ToList();

        Assert.Equal(LoadoutCount, mirrors.Count);
        Assert.All(mirrors, pair => Assert.Equal(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex));
        Assert.Equal(
            Enumerable.Range(0, LoadoutCount),
            mirrors.Select(pair => pair.FirstLoadoutIndex));
    }

    [Fact]
    public void OneVersusOnePairs_ResolveLoadoutsFromTheCanonicalList()
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;

        Assert.All(MovementScenarioMatrix.EnumerateOneVersusOnePairs(), pair =>
        {
            Assert.Equal(loadouts[pair.FirstLoadoutIndex], pair.FirstLoadout);
            Assert.Equal(loadouts[pair.SecondLoadoutIndex], pair.SecondLoadout);
        });
    }

    [Fact]
    public void TeamCompositions_AreTheSameTwentyOneIndexPairsAsTheOneVersusOnePairs()
    {
        var pairIndices = MovementScenarioMatrix
            .EnumerateOneVersusOnePairs()
            .Select(pair => (pair.FirstLoadoutIndex, pair.SecondLoadoutIndex))
            .ToList();

        var teamIndices = MovementScenarioMatrix
            .EnumerateTeamCompositions()
            .Select(team => (team.FirstMemberIndex, team.SecondMemberIndex))
            .ToList();

        Assert.Equal(PairCount, teamIndices.Count);
        Assert.Equal(pairIndices, teamIndices);
    }

    [Fact]
    public void TeamMatchups_CountExactlyTwoHundredThirtyOne()
    {
        Assert.Equal(MatchupCount, MovementScenarioMatrix.EnumerateTeamMatchups().Length);
    }

    [Fact]
    public void TeamMatchups_AreUnique()
    {
        var matchups = MovementScenarioMatrix.EnumerateTeamMatchups();

        Assert.Equal(matchups.Length, matchups.Distinct().Count());
    }

    [Fact]
    public void TeamMatchups_FollowTheCanonicalNestedEnumerationOrder()
    {
        // Independent oracle over team indices: teams in canonical order,
        // crossed with nested indices i <= j.
        var teams = MovementScenarioMatrix.EnumerateTeamCompositions();
        var expected = new List<(int FirstTeam, int SecondTeam)>();
        for (var i = 0; i < teams.Length; i++)
        {
            for (var j = i; j < teams.Length; j++)
            {
                expected.Add((i, j));
            }
        }

        var teamIndexByComposition = teams
            .Select((team, index) => (team, index))
            .ToDictionary(entry => entry.team, entry => entry.index);

        var actual = MovementScenarioMatrix
            .EnumerateTeamMatchups()
            .Select(matchup => (
                teamIndexByComposition[matchup.FirstTeam],
                teamIndexByComposition[matchup.SecondTeam]))
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TeamMatchups_ContainExactlyTwentyOneMirrors()
    {
        var mirrors = MovementScenarioMatrix
            .EnumerateTeamMatchups()
            .Where(matchup => matchup.IsMirror)
            .ToList();

        Assert.Equal(PairCount, mirrors.Count);
        Assert.All(mirrors, matchup => Assert.Equal(matchup.FirstTeam, matchup.SecondTeam));
        Assert.Equal(
            MovementScenarioMatrix.EnumerateTeamCompositions(),
            mirrors.Select(matchup => matchup.FirstTeam));
    }

    [Fact]
    public void ShieldedCellCombatPreset_IsPrecolonialPhilippinesV2()
    {
        Assert.Equal(
            CombatPresetId.PrecolonialPhilippinesV2,
            MovementScenarioMatrix.ShieldedCellCombatPreset);
    }

    [Fact]
    public void OneVersusOnePairs_FlagExactlyTheShieldedCellsAsRequiringV2()
    {
        var pairs = MovementScenarioMatrix.EnumerateOneVersusOnePairs();

        Assert.All(pairs, pair => Assert.Equal(
            pair.FirstLoadout.Shield != ShieldId.None
                || pair.SecondLoadout.Shield != ShieldId.None,
            pair.RequiresPrecolonialPhilippinesV2));

        // Four unshielded loadouts give C(4,2) + 4 = 10 unshielded cells,
        // so 21 - 10 = 11 cells contain a shielded loadout.
        Assert.Equal(11, pairs.Count(pair => pair.RequiresPrecolonialPhilippinesV2));
    }

    [Fact]
    public void TeamMatchups_FlagExactlyTheShieldedCellsAsRequiringV2()
    {
        var matchups = MovementScenarioMatrix.EnumerateTeamMatchups();

        Assert.All(matchups, matchup => Assert.Equal(
            matchup.FirstTeam.ContainsShieldedLoadout
                || matchup.SecondTeam.ContainsShieldedLoadout,
            matchup.RequiresPrecolonialPhilippinesV2));

        // Ten unshielded compositions give C(10,2) + 10 = 55 unshielded
        // matchups, so 231 - 55 = 176 matchups contain a shielded loadout.
        Assert.Equal(176, matchups.Count(matchup => matchup.RequiresPrecolonialPhilippinesV2));
    }

    [Fact]
    public void Enumerations_AreStableAcrossReRuns()
    {
        // ImmutableArray<T> equality is reference equality of the backing
        // array, so compare the two runs element by element.
        Assert.Equal(
            MovementScenarioMatrix.EnumerateOneVersusOnePairs().AsEnumerable(),
            MovementScenarioMatrix.EnumerateOneVersusOnePairs().AsEnumerable());
        Assert.Equal(
            MovementScenarioMatrix.EnumerateTeamCompositions().AsEnumerable(),
            MovementScenarioMatrix.EnumerateTeamCompositions().AsEnumerable());
        Assert.Equal(
            MovementScenarioMatrix.EnumerateTeamMatchups().AsEnumerable(),
            MovementScenarioMatrix.EnumerateTeamMatchups().AsEnumerable());
    }
}
