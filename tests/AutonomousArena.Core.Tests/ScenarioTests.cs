using AutonomousArena.Core.Mathematics;
using AutonomousArena.Core.Simulation;

namespace AutonomousArena.Core.Tests;

public sealed class ScenarioTests
{
    [Fact]
    public void CreateDefaultBuildsValidatedHundredVersusHundredScenario()
    {
        var scenario = Scenario.CreateDefault(seed: 42, totalAgents: 200);

        scenario.Validate();

        Assert.Equal(42UL, scenario.Seed);
        Assert.Equal(100, scenario.AgentsPerFaction);
        Assert.Equal(200, scenario.TotalAgents);
        Assert.Equal(20, scenario.TickRate);
        Assert.Equal(10_000, scenario.TickLimit);
    }

    [Fact]
    public void CreateDefaultRejectsInvalidTotalAgentCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Scenario.CreateDefault(totalAgents: 0));
        Assert.Throws<ArgumentException>(
            () => Scenario.CreateDefault(totalAgents: 201));
    }

    [Theory]
    [InlineData(0, 720, 20, 10_000)]
    [InlineData(1280, 0, 20, 10_000)]
    [InlineData(1280, 720, 0, 10_000)]
    [InlineData(1280, 720, 20, 0)]
    public void ValidateRejectsInvalidMapOrTickBounds(
        int mapWidth,
        int mapHeight,
        int tickRate,
        int tickLimit)
    {
        var scenario = Scenario.CreateDefault() with
        {
            MapWidth = mapWidth,
            MapHeight = mapHeight,
            TickRate = tickRate,
            TickLimit = tickLimit,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsNonpositiveFactionCount()
    {
        var scenario = Scenario.CreateDefault() with { AgentsPerFaction = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Theory]
    [InlineData(nameof(Scenario.MaximumHitPoints))]
    [InlineData(nameof(Scenario.DamagePerAttack))]
    [InlineData(nameof(Scenario.AttackRangeRaw))]
    [InlineData(nameof(Scenario.PerceptionRangeRaw))]
    [InlineData(nameof(Scenario.MovementSpeedRaw))]
    [InlineData(nameof(Scenario.AttackCooldownTicks))]
    public void ValidateRejectsNonpositiveCombatValues(string propertyName)
    {
        var original = Scenario.CreateDefault();
        var scenario = propertyName switch
        {
            nameof(Scenario.MaximumHitPoints) => original with { MaximumHitPoints = 0 },
            nameof(Scenario.DamagePerAttack) => original with { DamagePerAttack = 0 },
            nameof(Scenario.AttackRangeRaw) => original with { AttackRangeRaw = 0 },
            nameof(Scenario.PerceptionRangeRaw) => original with { PerceptionRangeRaw = 0 },
            nameof(Scenario.MovementSpeedRaw) => original with { MovementSpeedRaw = 0 },
            nameof(Scenario.AttackCooldownTicks) => original with { AttackCooldownTicks = 0 },
            _ => throw new InvalidOperationException($"Unknown property {propertyName}."),
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsValuesThatRiskFixedPointArithmeticOverflow()
    {
        var oversizedMap = Scenario.CreateDefault() with
        {
            MapWidth = Scenario.MaximumMapDimension + 1,
        };
        var excessivePopulation = Scenario.CreateDefault() with
        {
            AgentsPerFaction = (int.MaxValue / 2) + 1,
        };
        var excessiveRange = Scenario.CreateDefault() with
        {
            AttackRangeRaw = checked((Scenario.MaximumMapDimension * FixedPoint.Scale) + 1),
        };
        var excessiveAccumulatedDamage = Scenario.CreateDefault() with
        {
            AgentsPerFaction = Scenario.MaximumAgentsPerFaction,
            DamagePerAttack = 1_000_000,
        };

        Assert.Throws<ArgumentOutOfRangeException>(oversizedMap.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(excessivePopulation.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(excessiveRange.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(
            excessiveAccumulatedDamage.Validate);
    }

    [Fact]
    public void MinimumMapCreatesAgentsInsideValidatedBounds()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = 1,
            MapHeight = 1,
            AttackRangeRaw = 1,
            PerceptionRangeRaw = FixedPoint.Scale,
            MovementSpeedRaw = 1,
        };

        var simulation = BattleSimulation.Create(scenario);

        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(agent.XRaw, 0, FixedPoint.Scale);
                Assert.InRange(agent.YRaw, 0, FixedPoint.Scale);
            });
    }
}
