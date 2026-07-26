using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class BattleSimulationTests
{
    [Fact]
    public void NearestTargetUsesEntityIdToBreakDistanceTies()
    {
        var scenario = CreateTestScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 50, y: 50, scenario),
            CreateAgent(2, factionId: 1, x: 60, y: 50, scenario),
            CreateAgent(3, factionId: 1, x: 40, y: 50, scenario));

        simulation.AdvanceOneTick();

        var agent = Assert.Single(
            simulation.Agents,
            candidate => candidate.EntityId == 1);
        Assert.Equal(2UL, agent.TargetEntityId);
    }

    [Fact]
    public void AgentApproachesTargetByConfiguredFixedStep()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = FixedPoint.Scale,
            MovementSpeedRaw = 3 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 100, y: 10, scenario));

        simulation.AdvanceOneTick();

        var mover = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 1);
        Assert.Equal(13 * FixedPoint.Scale, mover.XRaw);
        Assert.Equal(10 * FixedPoint.Scale, mover.YRaw);
        Assert.Equal(AgentIntent.Moving, mover.Intent);
    }

    [Fact]
    public void AgentsAtExactRangeAttackAndRespectCooldown()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
            DamagePerAttack = 10,
            AttackCooldownTicks = 2,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.All(simulation.Agents, agent => Assert.Equal(90, agent.HitPoints));
        Assert.Equal(
            2,
            simulation.LastEvents.Count(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack));

        simulation.AdvanceOneTick();

        Assert.All(simulation.Agents, agent => Assert.Equal(90, agent.HitPoints));
        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack);

        simulation.AdvanceOneTick();

        Assert.All(simulation.Agents, agent => Assert.Equal(80, agent.HitPoints));
    }

    [Fact]
    public void LastEventsRemainsACompletedTickSnapshot()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
            AttackCooldownTicks = 2,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        simulation.AdvanceOneTick();
        var retainedEvents = simulation.LastEvents;
        var expectedEvents = retainedEvents.ToArray();

        simulation.AdvanceOneTick();

        Assert.NotEmpty(expectedEvents);
        Assert.Empty(simulation.LastEvents);
        Assert.Equal(expectedEvents, retainedEvents);
    }

    [Fact]
    public void DamageIsAccumulatedBeforeMutualDeathResolution()
    {
        var scenario = CreateTestScenario() with
        {
            MaximumHitPoints = 10,
            DamagePerAttack = 10,
            AttackRangeRaw = 20 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 20, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.All(simulation.Agents, agent => Assert.False(agent.IsAlive));
        Assert.Equal(BattleOutcome.Draw, simulation.Outcome);
        Assert.Equal(
            [1UL, 2UL],
            simulation.LastEvents
                .Where(battleEvent => battleEvent.Kind == BattleEventKind.Death)
                .Select(battleEvent => battleEvent.SourceEntityId));
    }

    [Fact]
    public void DeadAgentsNeverSelectTargetsMoveOrAttack()
    {
        var scenario = CreateTestScenario();
        var deadAgent = CreateAgent(1, factionId: 0, x: 10, y: 10, scenario);
        deadAgent.HitPoints = 0;
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            deadAgent,
            CreateAgent(2, factionId: 0, x: 15, y: 10, scenario),
            CreateAgent(3, factionId: 1, x: 100, y: 10, scenario));

        simulation.AdvanceOneTick();

        var deadView = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 1);
        Assert.Null(deadView.TargetEntityId);
        Assert.Equal(10 * FixedPoint.Scale, deadView.XRaw);
        Assert.Equal(AgentIntent.Dead, deadView.Intent);
        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.SourceEntityId == 1 &&
                battleEvent.Kind is BattleEventKind.Move or BattleEventKind.Attack);
    }

    [Fact]
    public void VictoryIsEmittedExactlyOnce()
    {
        var scenario = CreateTestScenario() with
        {
            MaximumHitPoints = 10,
            DamagePerAttack = 10,
            AttackRangeRaw = 20 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 0, x: 12, y: 10, scenario),
            CreateAgent(3, factionId: 1, x: 20, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(BattleOutcome.Faction0Victory, simulation.Outcome);
        var outcomeEvent = Assert.Single(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Outcome);
        Assert.Equal(0, outcomeEvent.FactionId);

        simulation.AdvanceOneTick();

        Assert.Empty(simulation.LastEvents);
        Assert.Equal(1, simulation.Tick);
    }

    [Fact]
    public void RepeatedQuietTicksHaveBoundedAllocations()
    {
        const int measuredTicks = 1_000;
        const long maximumAllocatedBytes = 300_000;
        var scenario = CreateTestScenario() with
        {
            TickLimit = measuredTicks + 100,
            AttackRangeRaw = FixedPoint.Scale,
            PerceptionRangeRaw = 5 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 190, y: 90, scenario));

        for (var tick = 0; tick < 32; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBytes =
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(
            allocatedBytes <= maximumAllocatedBytes,
            $"Quiet ticks allocated {allocatedBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");
    }

    [Fact]
    public void SeedsOneThroughTwentyProduceVictoriesForBothFactions()
    {
        var outcomes = new HashSet<BattleOutcome>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: 200);
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing)
            {
                simulation.AdvanceOneTick();
            }

            outcomes.Add(simulation.Outcome);
        }

        Assert.Contains(BattleOutcome.Faction0Victory, outcomes);
        Assert.Contains(BattleOutcome.Faction1Victory, outcomes);
    }

    private static Scenario CreateTestScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 100,
            DamagePerAttack = 10,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            MovementSpeedRaw = FixedPoint.Scale,
            AttackCooldownTicks = 1,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int x,
        int y,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            checked(x * FixedPoint.Scale),
            checked(y * FixedPoint.Scale),
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks);
}
