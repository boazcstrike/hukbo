using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class MatchSummaryFactoryTests
{
    [Fact]
    public void Create_LabelsFactionZeroVictoryAndCountsLivingAgents()
    {
        AgentView[] agents =
        [
            CreateAgent(1, factionId: 0, isAlive: true),
            CreateAgent(2, factionId: 0, isAlive: false),
            CreateAgent(3, factionId: 1, isAlive: true),
        ];

        var summary = MatchSummaryFactory.Create(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 20,
            tickRate: 20,
            seed: 1);

        Assert.Equal("Blue", summary.WinnerLabel);
        Assert.Equal(1, summary.BlueSurvivors);
        Assert.Equal(1, summary.RedSurvivors);
    }

    [Fact]
    public void Create_LabelsFactionOneVictory()
    {
        var summary = MatchSummaryFactory.Create(
            BattleOutcome.Faction1Victory,
            Array.Empty<AgentView>(),
            tick: 20,
            tickRate: 20,
            seed: 1);

        Assert.Equal("Red", summary.WinnerLabel);
    }

    [Fact]
    public void Create_LabelsDraw()
    {
        var summary = MatchSummaryFactory.Create(
            BattleOutcome.Draw,
            Array.Empty<AgentView>(),
            tick: 20,
            tickRate: 20,
            seed: 1);

        Assert.Equal("Draw", summary.WinnerLabel);
    }

    [Fact]
    public void Create_CalculatesExactSimulatedDuration()
    {
        var summary = MatchSummaryFactory.Create(
            BattleOutcome.Draw,
            Array.Empty<AgentView>(),
            tick: 7,
            tickRate: 4,
            seed: 1);

        Assert.Equal(1.75, summary.SimulatedDurationSeconds);
    }

    [Fact]
    public void Create_PassesThroughTerminalTickAndSeed()
    {
        var summary = MatchSummaryFactory.Create(
            BattleOutcome.Draw,
            Array.Empty<AgentView>(),
            tick: 235,
            tickRate: 20,
            seed: 0xDEADBEEF);

        Assert.Equal(235, summary.TerminalTick);
        Assert.Equal(0xDEADBEEFUL, summary.Seed);
    }

    [Fact]
    public void Create_RejectsOngoingOutcome()
    {
        Assert.Throws<ArgumentException>(
            () => MatchSummaryFactory.Create(
                BattleOutcome.Ongoing,
                Array.Empty<AgentView>(),
                tick: 0,
                tickRate: 20,
                seed: 1));
    }

    [Fact]
    public void Create_RejectsInvalidTickRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MatchSummaryFactory.Create(
                BattleOutcome.Draw,
                Array.Empty<AgentView>(),
                tick: 0,
                tickRate: 0,
                seed: 1));
    }

    private static AgentView CreateAgent(
        ulong entityId,
        int factionId,
        bool isAlive) =>
        new(
            entityId,
            factionId,
            XRaw: 0,
            YRaw: 0,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: isAlive ? AgentIntent.Idle : AgentIntent.Dead,
            isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
