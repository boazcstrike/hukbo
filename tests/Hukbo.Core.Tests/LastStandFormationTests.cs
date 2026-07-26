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

    [Fact]
    public void ARegroupingFollowerMovesTowardTheRallyAgentPlusItsOffset()
    {
        // Rally straight above the follower; the enemy is off to the side, so
        // "toward the rally point" (near-zero X movement) and "toward the
        // enemy" (large positive X movement) are trivially distinguishable.
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 1000, y: 1000, scenario),
            CreateAgent(5, factionId: 0, x: 1000, y: 1980, scenario),
            CreateAgent(100, factionId: 1, x: 1200, y: 1200, scenario));

        var before = AgentByEntityId(simulation, 5);

        simulation.AdvanceOneTick();

        var after = AgentByEntityId(simulation, 5);
        Assert.Equal(AgentIntent.Regrouping, after.Intent);
        Assert.True(
            after.YRaw < before.YRaw,
            "Expected the follower to move up, toward its rally agent.");
        Assert.True(
            Math.Abs(after.XRaw - before.XRaw) <= 8,
            "Expected near-zero X movement toward the rally point, not the " +
            $"large positive X movement heading toward the enemy. Delta was " +
            $"{after.XRaw - before.XRaw} raw units.");
    }

    [Fact]
    public void ARegroupingFollowerAlreadyAtItsAimPointProposesNoMovementAndEmitsNoMoveEvent()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var rallyXRaw = checked(1000 * FixedPoint.Scale);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            scenario.Seed,
            entityId: 5,
            scenario.BodyRadiusRaw);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            checked(rallyXRaw + offsetXRaw),
            mapWidthRaw,
            scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            checked(rallyYRaw + offsetYRaw),
            mapHeightRaw,
            scenario.BodyRadiusRaw);

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, factionId: 0, rallyXRaw, rallyYRaw, scenario),
            CreateAgentAtRawPosition(5, factionId: 0, aimXRaw, aimYRaw, scenario),
            CreateAgent(100, factionId: 1, x: 1200, y: 1200, scenario));

        simulation.AdvanceOneTick();

        Assert.DoesNotContain(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move && evt.SourceEntityId == 5);
        Assert.Equal(
            MovementResolution.None,
            AgentByEntityId(simulation, 5).MovementResolution);
    }

    [Fact]
    public void AMoveEventFromARegroupingFollowerNamesTheRallyAgentAsItsTarget()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 1000, y: 1000, scenario),
            CreateAgent(5, factionId: 0, x: 1000, y: 1980, scenario),
            CreateAgent(100, factionId: 1, x: 1200, y: 1200, scenario));

        simulation.AdvanceOneTick();

        var moveEvent = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move && evt.SourceEntityId == 5);
        Assert.Equal(2UL, moveEvent.TargetEntityId);
    }

    [Fact]
    public void ARegroupingFollowerStillAttacksAnEnemyInsideReach()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var followerXRaw = checked(500 * FixedPoint.Scale);
        var followerYRaw = checked(10 * FixedPoint.Scale);
        // Well within CreateTestScenario's contact distance
        // (2 * BodyRadiusRaw = 1024 raw), so the follower is already
        // Attacking, never Regrouping, before movement is even gathered.
        var enemyXRaw = checked(followerXRaw + 500);

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            // Rally agent far away: proves the attack does not wait for the
            // follower to close on it first.
            CreateAgent(2, factionId: 0, x: 10, y: 10, scenario),
            CreateAgentAtRawPosition(5, factionId: 0, followerXRaw, followerYRaw, scenario),
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, followerYRaw, scenario));

        simulation.AdvanceOneTick();

        var attackEvent = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Attack && evt.SourceEntityId == 5);
        Assert.Equal(100UL, attackEvent.TargetEntityId);
        Assert.Equal(
            AgentIntent.Attacking,
            AgentByEntityId(simulation, 5).Intent);
    }

    [Fact]
    public void AnAimPointOutsideTheMapIsClampedInsideTheBounds()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        // A rally position far beyond the right edge of the map. Its own
        // creation is unclamped (CreateForTesting does not resolve spawn
        // placement), but a follower's aim point must still land inside the
        // map, not off toward this raw coordinate.
        var farOutsideRallyXRaw = checked(mapWidthRaw + 500_000);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            scenario.Seed,
            entityId: 5,
            scenario.BodyRadiusRaw);
        var clampedAimXRaw = CollisionGeometry.ClampCenterToBounds(
            checked(farOutsideRallyXRaw + offsetXRaw),
            mapWidthRaw,
            scenario.BodyRadiusRaw);
        var clampedAimYRaw = CollisionGeometry.ClampCenterToBounds(
            checked(rallyYRaw + offsetYRaw),
            mapHeightRaw,
            scenario.BodyRadiusRaw);

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(
                2, factionId: 0, farOutsideRallyXRaw, rallyYRaw, scenario),
            // Placed exactly at the clamped aim point: if the aim point were
            // not clamped before the arrived-guard's distance check, the
            // follower would see itself as enormously far from the (unclamped,
            // off-map) point and would propose real movement instead of none.
            CreateAgentAtRawPosition(
                5, factionId: 0, clampedAimXRaw, clampedAimYRaw, scenario),
            CreateAgent(100, factionId: 1, x: 1200, y: 1200, scenario));

        simulation.AdvanceOneTick();

        var after = AgentByEntityId(simulation, 5);
        Assert.Equal(clampedAimXRaw, after.XRaw);
        Assert.Equal(clampedAimYRaw, after.YRaw);
        Assert.True(after.XRaw <= mapWidthRaw - scenario.BodyRadiusRaw);
        Assert.True(after.XRaw >= scenario.BodyRadiusRaw);
    }

    [Fact]
    public void LastStandRallyDrawsDoNotChangeSpawnPositions()
    {
        var withoutRally = Scenario.CreateDefault(seed: 42, totalAgents: 20) with
        {
            LastStandThresholdAgents = 0,
        };
        var withRally = Scenario.CreateDefault(seed: 42, totalAgents: 20) with
        {
            LastStandThresholdAgents = 6,
        };

        var simulationWithoutRally = BattleSimulation.Create(withoutRally);
        var simulationWithRally = BattleSimulation.Create(withRally);

        var withoutRallyPositions = simulationWithoutRally.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => (agent.XRaw, agent.YRaw));

        foreach (var agent in simulationWithRally.Agents)
        {
            Assert.Equal(
                withoutRallyPositions[agent.EntityId],
                (agent.XRaw, agent.YRaw));
        }
    }

    private static AgentState CreateAgentAtRawPosition(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None));

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
