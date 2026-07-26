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
        // The rally agent (2) and its enemy target (100) share a Y
        // coordinate, so the direction of travel is purely +X and the trail
        // computation needs no square root: distance == dx exactly.
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var rallyXRaw = checked(1000 * FixedPoint.Scale);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var enemyXRaw = checked(1200 * FixedPoint.Scale);
        var trailRaw = FormationRules.ComputeRallyTrailRaw(scenario.BodyRadiusRaw);
        var trailBaseXRaw = checked(rallyXRaw - trailRaw);
        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            scenario.Seed,
            entityId: 5,
            scenario.BodyRadiusRaw);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            checked(trailBaseXRaw + offsetXRaw),
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
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, rallyYRaw, scenario));

        simulation.AdvanceOneTick();

        Assert.DoesNotContain(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move && evt.SourceEntityId == 5);
        Assert.Equal(
            MovementResolution.None,
            AgentByEntityId(simulation, 5).MovementResolution);
    }

    [Fact]
    public void AFollowerAimsBehindTheRallyAgentRelativeToItsDirectionOfTravel()
    {
        // Rally agent (2) and its target (100) share a Y coordinate, so the
        // direction of travel is purely +X and the trail computation needs
        // no square root: distance == dx exactly. Placing the enemy inside
        // contact range holds the rally agent's own position (and thus its
        // direction of travel) steady while the follower's aim formula is
        // evaluated.
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var rallyXRaw = checked(500 * FixedPoint.Scale);
        var rallyYRaw = checked(10 * FixedPoint.Scale);
        var enemyXRaw = checked(rallyXRaw + (2 * scenario.BodyRadiusRaw) - 1);

        var trailRaw = FormationRules.ComputeRallyTrailRaw(scenario.BodyRadiusRaw);
        var expectedTrailBaseXRaw = checked(rallyXRaw - trailRaw);

        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            scenario.Seed,
            entityId: 5,
            scenario.BodyRadiusRaw);
        var expectedAimXRaw = CollisionGeometry.ClampCenterToBounds(
            checked(expectedTrailBaseXRaw + offsetXRaw),
            mapWidthRaw,
            scenario.BodyRadiusRaw);
        var expectedAimYRaw = CollisionGeometry.ClampCenterToBounds(
            checked(rallyYRaw + offsetYRaw),
            mapHeightRaw,
            scenario.BodyRadiusRaw);

        // The trail (12R) always exceeds the maximum jitter magnitude (6R),
        // so the aim point's projection along the leader-to-target direction
        // (here, the raw X axis) is always negative — the aim point always
        // sits behind the rally agent, regardless of this follower's own
        // deterministic jitter draw.
        Assert.True(
            expectedAimXRaw < rallyXRaw,
            "Expected the follower's aim point to sit behind the rally " +
            $"agent (aim X < rally X = {rallyXRaw}), but computed aim X " +
            $"was {expectedAimXRaw}.");

        // Placing the follower exactly at the independently computed aim
        // point and confirming the arrived-guard fires proves this
        // computation matches BuildRegroupingProposal's own aim point, not
        // just an assertion about the test's private arithmetic.
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, factionId: 0, rallyXRaw, rallyYRaw, scenario),
            CreateAgentAtRawPosition(
                5, factionId: 0, expectedAimXRaw, expectedAimYRaw, scenario),
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, rallyYRaw, scenario));

        simulation.AdvanceOneTick();

        var follower = AgentByEntityId(simulation, 5);
        Assert.Equal(AgentIntent.Regrouping, follower.Intent);
        Assert.Equal(MovementResolution.None, follower.MovementResolution);
    }

    [Fact]
    public void ARallyAgentWithNoTargetStillGathersItsFollowers()
    {
        // A tight 50-world-unit perception range puts the enemy inside the
        // follower's view (40 units away) but outside the rally agent's (940
        // units away), so the rally agent has no target at all. The trail
        // fallback in that case is the rally agent's raw position (no
        // trail), matching the pre-fix formula.
        var baseScenario = CreateTestScenario(lastStandThreshold: 2);
        var scenario = baseScenario with
        {
            PerceptionRangeRaw = checked(50 * FixedPoint.Scale),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(2, factionId: 0, x: 1000, y: 1000, scenario),
            CreateAgent(5, factionId: 0, x: 1000, y: 1900, scenario),
            CreateAgent(100, factionId: 1, x: 1000, y: 1940, scenario));

        var before = AgentByEntityId(simulation, 5);

        simulation.AdvanceOneTick();

        var rally = AgentByEntityId(simulation, 2);
        var after = AgentByEntityId(simulation, 5);

        Assert.Null(rally.TargetEntityId);
        Assert.Equal(AgentIntent.Regrouping, after.Intent);
        Assert.True(
            after.YRaw < before.YRaw,
            "Expected the follower to still move toward its rally agent " +
            "even though the rally agent has no target of its own (the " +
            "no-target trail fallback).");
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
        // The enemy is far enough away (beyond the rally agent's perception
        // range) that the rally agent has no target, so the trail fallback
        // applies: the trail base is the rally agent's own raw position, no
        // trail term, matching the pre-fix formula exactly.
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

    /// <summary>
    /// Task 7 coverage: locks the feature against liveness, flap, and
    /// packing regressions once the rally-agent selection (task 5) and the
    /// aim-point movement (task 6) are both in place.
    /// </summary>
    [Fact]
    public void BothFactionsInASixVersusSixLastStandReachATerminalOutcome()
    {
        // Twelve total agents means six per faction, exactly
        // FormationRules.DefaultLastStandThresholdAgents, so both factions
        // are already in their last stand at tick zero. This is the
        // anti-standoff lock for design risk R5: the rally agent is exempt
        // from the formation and keeps chasing the nearest enemy under the
        // ordinary movement rule, so at least one warrior per side is always
        // closing and the battle can never settle into two clusters that
        // only the tick limit ends.
        const long WellInsideTheTickLimit = 2_000;
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 12);
        var simulation = BattleSimulation.Create(scenario);

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < WellInsideTheTickLimit)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Outcome != BattleOutcome.Ongoing,
            simulation.Outcome == BattleOutcome.Ongoing
                ? "The 6v6 last stand never reached a terminal outcome " +
                  $"within {WellInsideTheTickLimit} ticks, well inside the " +
                  $"{scenario.TickLimit}-tick limit. Both clusters appear " +
                  "to have stalled, which is exactly the standoff design " +
                  "risk R5 guards against."
                : $"Reached terminal outcome {simulation.Outcome} at tick " +
                  $"{simulation.Tick}.");
    }

    [Fact]
    public void LivingCountsNeverIncreaseAcrossAWholeBattle()
    {
        // Proves design risk R2 by construction: hit points are only ever
        // written as Math.Max(0, hp - damage) and nothing revives an agent,
        // so a faction's living count must be monotone non-increasing. If
        // this test can ever fail, the last-stand trigger could flap.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200);
        var simulation = BattleSimulation.Create(scenario);
        var previousLivingCounts = new[] { int.MaxValue, int.MaxValue };

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            var livingCounts = new[]
            {
                simulation.Agents.Count(agent =>
                    agent.FactionId == 0 && agent.IsAlive),
                simulation.Agents.Count(agent =>
                    agent.FactionId == 1 && agent.IsAlive),
            };

            for (var faction = 0; faction < 2; faction++)
            {
                Assert.True(
                    livingCounts[faction] <= previousLivingCounts[faction],
                    $"Faction {faction}'s living count rose from " +
                    $"{previousLivingCounts[faction]} to " +
                    $"{livingCounts[faction]} at tick {simulation.Tick}. A " +
                    "rising living count means the last-stand trigger could " +
                    "flap (design risk R2).");
            }

            previousLivingCounts = livingCounts;
        }

        Assert.NotEqual(BattleOutcome.Ongoing, simulation.Outcome);
    }

    [Fact]
    public void RallyAgentDeathPromotesTheNextLowestLivingEntityId()
    {
        // Entities 2, 5, and 9 all sit far from entity 100, so nobody is
        // Attacking on tick 1 and the follower positions have barely moved
        // toward the old rally point (2) by the time it dies. That leaves
        // plenty of separation between the old rally point and the new one
        // (5's tick-start position), so follower 9's aim point necessarily
        // moves and a Move event is guaranteed on tick 2.
        var scenario = CreateTestScenario(lastStandThreshold: 3);
        var rallyAgentState = CreateAgent(2, factionId: 0, x: 10, y: 10, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rallyAgentState,
            CreateAgent(5, factionId: 0, x: 20, y: 10, scenario),
            CreateAgent(9, factionId: 0, x: 30, y: 10, scenario),
            CreateAgent(100, factionId: 1, x: 300, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.NotEqual(AgentIntent.Regrouping, AgentByEntityId(simulation, 2).Intent);
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 5).Intent);
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 9).Intent);

        // Kill the current rally agent (entity 2) between ticks. The array
        // passed to CreateForTesting holds these exact AgentState
        // references, so mutating this field reaches the simulation's
        // internal state the same way the existing dead-agent tests do.
        rallyAgentState.HitPoints = 0;

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Dead, AgentByEntityId(simulation, 2).Intent);
        var promotedRally = AgentByEntityId(simulation, 5);
        Assert.True(
            promotedRally.Intent is AgentIntent.Moving or AgentIntent.Attacking,
            "Expected entity 5, the next-lowest living EntityId, to be " +
            $"promoted to rally agent and keep an ordinary intent, got " +
            $"{promotedRally.Intent}.");
        Assert.Equal(AgentIntent.Regrouping, AgentByEntityId(simulation, 9).Intent);

        var reaimedMoveEvent = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move && evt.SourceEntityId == 9);
        Assert.Equal(
            5UL,
            reaimedMoveEvent.TargetEntityId);
    }

    [Fact]
    public void AMaximumSizedLastStandNeverLeavesAWarriorBlockedForMoreThanSixtyConsecutiveTicks()
    {
        const int MaximumAllowedBlockedStreakTicks = 60;
        // Sixteen agents per faction is FormationRules.MaximumLastStandThresholdAgents,
        // the square-packing bound, so both factions are the most tightly
        // clustered configuration the design permits from tick zero.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 32) with
        {
            LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
        };
        var simulation = BattleSimulation.Create(scenario);

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        var livingFaction0 = simulation.Agents.Count(
            agent => agent.FactionId == 0 && agent.IsAlive);
        var livingFaction1 = simulation.Agents.Count(
            agent => agent.FactionId == 1 && agent.IsAlive);
        Assert.True(
            simulation.LongestBlockedStreakTicks <= MaximumAllowedBlockedStreakTicks,
            "Longest observed blocked streak was " +
            $"{simulation.LongestBlockedStreakTicks} ticks, exceeding the " +
            $"{MaximumAllowedBlockedStreakTicks}-tick bound. A failure here " +
            "means the last-stand cluster packs tighter than the collision " +
            "resolver permits (design risk R4). Diagnostics: stopped at " +
            $"tick {simulation.Tick} of {scenario.TickLimit}, outcome " +
            $"{simulation.Outcome}, living counts " +
            $"[{livingFaction0}, {livingFaction1}].");
    }

    /// <summary>
    /// Load-bearing regression lock for the follower-trailing fix. Before
    /// the fix, a follower whose jitter offset pointed along its rally
    /// agent's own direction of travel could park directly in front of that
    /// rally agent and block it forever; two factions doing this at once
    /// deadlocked the whole battle at the tick limit with zero casualties.
    /// Observed concretely, before the fix: seed 5 stalled at a threshold of
    /// 6, and seeds 2 and 6 stalled at a threshold of 9. Every seed here
    /// runs at <see cref="FormationRules.MaximumLastStandThresholdAgents"/>,
    /// the tightest formation the design permits, so this is the worst case
    /// for the deadlock this test guards against.
    /// </summary>
    [Fact]
    public void NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty()
    {
        const int TotalAgents = 18;
        var stalledSeeds = new List<string>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: TotalAgents) with
            {
                LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
            };
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            // A battle that only reaches its terminal outcome because the
            // tick limit forces one (see BattleSimulation's Tick >=
            // TickLimit => Draw rule) is a stall, regardless of what the
            // forced outcome reports. Reaching the tick limit at all — not
            // the specific Outcome value — is the failure signal.
            if (simulation.Tick < scenario.TickLimit)
            {
                continue;
            }

            var livingFaction0 = simulation.Agents.Count(
                agent => agent.FactionId == 0 && agent.IsAlive);
            var livingFaction1 = simulation.Agents.Count(
                agent => agent.FactionId == 1 && agent.IsAlive);
            stalledSeeds.Add(
                $"seed {seed}: stalled at tick {simulation.Tick} of " +
                $"{scenario.TickLimit}, outcome {simulation.Outcome}, " +
                $"living counts [{livingFaction0}, {livingFaction1}], " +
                "longest blocked streak " +
                $"{simulation.LongestBlockedStreakTicks} ticks.");
        }

        Assert.True(
            stalledSeeds.Count == 0,
            "The following seeds never reached a terminal outcome before " +
            $"the tick limit:\n{string.Join('\n', stalledSeeds)}");
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
