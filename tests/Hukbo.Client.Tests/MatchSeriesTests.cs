using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class MatchSeriesTests
{
    [Fact]
    public void StartsWithNoWinsAtTheInitialSeed()
    {
        var series = new MatchSeries(initialSeed: 1);

        Assert.Equal(0, series.TeamAWins);
        Assert.Equal(0, series.TeamBWins);
        Assert.Equal(1UL, series.CurrentSeed);
    }

    [Theory]
    [InlineData(BattleOutcome.Faction0Victory, 1, 0)]
    [InlineData(BattleOutcome.Faction1Victory, 0, 1)]
    [InlineData(BattleOutcome.Draw, 0, 0)]
    [InlineData(BattleOutcome.Ongoing, 0, 0)]
    public void StartNextRound_RecordsOnlyTerminalVictories(
        BattleOutcome outcome,
        int expectedTeamAWins,
        int expectedTeamBWins)
    {
        var series = new MatchSeries(initialSeed: 1);

        series.StartNextRound(outcome);

        Assert.Equal(expectedTeamAWins, series.TeamAWins);
        Assert.Equal(expectedTeamBWins, series.TeamBWins);
    }

    [Fact]
    public void StartNextRound_AdvancesToDistinctDeterministicSeeds()
    {
        var left = new MatchSeries(initialSeed: 1);
        var right = new MatchSeries(initialSeed: 1);

        left.StartNextRound(BattleOutcome.Draw);
        right.StartNextRound(BattleOutcome.Draw);
        var firstSeed = left.CurrentSeed;

        left.StartNextRound(BattleOutcome.Ongoing);
        right.StartNextRound(BattleOutcome.Ongoing);

        Assert.NotEqual(1UL, firstSeed);
        Assert.NotEqual(firstSeed, left.CurrentSeed);
        Assert.Equal(left.CurrentSeed, right.CurrentSeed);
    }

    [Fact]
    public void FullReset_ClearsWinsAndRestoresTheInitialSeed()
    {
        const ulong initialSeed = ulong.MaxValue;
        var series = new MatchSeries(initialSeed);
        series.StartNextRound(BattleOutcome.Faction0Victory);
        series.StartNextRound(BattleOutcome.Faction1Victory);

        series.FullReset();

        Assert.Equal(0, series.TeamAWins);
        Assert.Equal(0, series.TeamBWins);
        Assert.Equal(initialSeed, series.CurrentSeed);
    }

    [Fact]
    public void FullReset_IsIdempotent()
    {
        var series = new MatchSeries(initialSeed: 1);
        series.StartNextRound(BattleOutcome.Faction0Victory);

        series.FullReset();
        series.FullReset();

        Assert.Equal(0, series.TeamAWins);
        Assert.Equal(0, series.TeamBWins);
        Assert.Equal(1UL, series.CurrentSeed);
    }
}
