using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Task 5 coverage: rally-agent selection and the
/// <see cref="AgentIntent.Regrouping"/> assignment in
/// <c>BattleSimulation.SelectTargetsAndIntents</c>. Movement toward the aim
/// point is Task 6 and is not exercised here; a regrouping agent in these
/// tests simply does not move.
/// </summary>
public sealed class LastStandFormationTests
{
    [Fact]
    public void TheLowestLivingEntityIdIsTheRallyAgentForItsFaction()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 3);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 0, x: 20, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.NotEqual(AgentIntent.Regrouping, AgentByEntityId(simulation, 2).Intent);
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 5).Intent);
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 9).Intent);
    }

    [Fact]
    public void ADeadAgentIsNeverTheRallyAgent()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 3);
        var deadAgent = CreateAgent(2, factionId: 0, x: 20, y: 10, scenario);
        deadAgent.HitPoints = 0;
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            deadAgent,
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Dead, AgentByEntityId(simulation, 2).Intent);
        Assert.NotEqual(AgentIntent.Regrouping, AgentByEntityId(simulation, 5).Intent);
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 9).Intent);
    }

    [Fact]
    public void TheRallyAgentKeepsOrdinaryNearestEnemyIntent()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 3);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 500, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 501, y: 10, scenario));

        simulation.AdvanceOneTick();

        var rally = AgentByEntityId(simulation, 2);
        Assert.True(
            rally.Intent is AgentIntent.Moving or AgentIntent.Attacking,
            $"Expected the rally agent to keep Moving or Attacking, got {rally.Intent}.");
        Assert.NotEqual(AgentIntent.Regrouping, rally.Intent);
    }

    [Fact]
    public void AFollowerBelowTheThresholdIsMarkedRegrouping()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 5).Intent);
    }

    [Fact]
    public void AFollowerWithinContactOfItsEnemyIsMarkedAttackingRatherThanRegrouping()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 500, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 501, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Attacking, AgentByEntityId(simulation, 5).Intent);
    }

    [Fact]
    public void AFactionAboveTheThresholdIsUnaffected()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 20, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(11, factionId: 0, x: 40, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.DoesNotContain(
            simulation.Agents,
            agent => agent.FactionId == 0 && agent.Intent == AgentIntent.Regrouping);
    }

    [Fact]
    public void EachFactionTriggersIndependently()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            // Faction 0: two living agents, at or below the threshold.
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 30, y: 10, scenario),
            // Faction 1: four living agents, above the threshold.
            CreateAgent(200, factionId: 1, x: 310, y: 10, scenario),
            CreateAgent(201, factionId: 1, x: 320, y: 10, scenario),
            CreateAgent(202, factionId: 1, x: 330, y: 10, scenario),
            CreateAgent(203, factionId: 1, x: 340, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 5).Intent);
        Assert.DoesNotContain(
            simulation.Agents,
            agent => agent.FactionId == 1 && agent.Intent == AgentIntent.Regrouping);
    }

    [Fact]
    public void AZeroThresholdDisablesTheFormationEntirely()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 0);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.DoesNotContain(
            simulation.Agents,
            agent => agent.Intent == AgentIntent.Regrouping);
    }

    [Fact]
    public void RallyAgentSelectionIsUnchangedByAgentArrayPermutation()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 3);

        var orderingA = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 0, x: 20, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));
        var orderingB = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 0, x: 20, y: 10, scenario));
        var orderingC = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 20, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario),
            CreateAgent(5, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario));

        orderingA.AdvanceOneTick();
        orderingB.AdvanceOneTick();
        orderingC.AdvanceOneTick();

        var intentsA = IntentsByEntityId(orderingA);
        var intentsB = IntentsByEntityId(orderingB);
        var intentsC = IntentsByEntityId(orderingC);

        Assert.Equal(intentsA, intentsB);
        Assert.Equal(intentsA, intentsC);
        Assert.Equal(orderingA.ComputeStateHash(), orderingB.ComputeStateHash());
        Assert.Equal(orderingA.ComputeStateHash(), orderingC.ComputeStateHash());
    }

    [Fact]
    public void ASingleSurvivorIsItsOwnRallyAgentAndBehavesExactlyAsBefore()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 3);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        var survivor = AgentByEntityId(simulation, 2);
        Assert.True(
            survivor.Intent is AgentIntent.Moving or AgentIntent.Attacking,
            $"Expected the sole survivor to keep Moving or Attacking, got " +
            $"{survivor.Intent}.");
    }

    private static Dictionary<ulong, AgentIntent> IntentsByEntityId(
        BattleSimulation simulation) =>
        simulation.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => agent.Intent);

    private static AgentView AgentByEntityId(
        BattleSimulation simulation,
        ulong entityId) =>
        Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == entityId);

    private static Scenario CreateTestScenario(int lastStandThreshold) =>
        new(
            Seed: 1,
            MapWidth: 2000,
            MapHeight: 2000,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 100,
            DamagePerAttack = 10,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            LastStandThresholdAgents = lastStandThreshold,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int x,
        int y,
        Scenario scenario,
        CombatLoadout? loadout = null) =>
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
            scenario.AttackCooldownTicks,
            loadout ?? new CombatLoadout(
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                ShieldId.None));
}
