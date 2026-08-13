using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
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

    /// <summary>
    /// PROVISIONAL tuning bound, not a measured property of the collision
    /// resolver. It guards design risk R4: permanent thrashing that produces a
    /// no-casualty draw at the tick limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bound was 60 when this case measured 45 on seed 1 alone. The weapon
    /// clash raised it to 69 by lengthening battles rather than by changing
    /// anything about collision: running this same scenario at the same commit
    /// through the ruleset seam with <c>ClashProfile.Neutral</c> reproduces 45
    /// exactly, which is the evidence that the collision resolver, the
    /// last-stand formation, and the collision priority amendment are all
    /// untouched. Interception means fewer landed blows per exchange, so a
    /// maximally packed cluster stays packed for longer.
    /// </para>
    /// <para>
    /// Seed 1 turned out to be a 25th-percentile seed for this metric, so the
    /// assertion now sweeps twenty seeds and takes the worst, following
    /// <see cref="NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred"/>
    /// in this file. Across seeds 1 to 20 the streak runs 59 to 92 with a
    /// median of 74; 125 is 1.36 times the worst observed, the same headroom
    /// the original 60 had over its measured 45.
    /// </para>
    /// <para>
    /// The bound stays two orders of magnitude below the R4 signal, so it loses
    /// no detection power: a genuinely permanent block runs into the thousands
    /// as it approaches the tick limit. Across the same twenty seeds no battle
    /// reached the tick limit, none drew, and none ended without casualties.
    /// </para>
    /// <para>
    /// This sweep stayed at twenty seeds when
    /// <see cref="NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred"/>
    /// beside it was widened to two hundred, and the reason is measured rather
    /// than assumed. Widening this one to two hundred fails: the worst blocked
    /// streak across 200 seeds is 272 ticks on seed 196, well over the 125-tick
    /// bound. That is not a stall — seed 196 ends in a normal victory at tick
    /// 1073 with living counts [2, 0] — it is a long transient block in a battle
    /// that resolves perfectly well. The 125-tick bound was fitted to the worst
    /// of twenty seeds (92) and is simply too tight for a larger sample.
    /// </para>
    /// <para>
    /// Widening this sweep therefore means re-deriving a PROVISIONAL
    /// game-design bound from a 200-seed sample, which is a tuning decision with
    /// a real cost: raising the bound toward 272 spends some of the two orders
    /// of magnitude of headroom that give it its detection power. That decision
    /// has not been taken, so the sweep is left at twenty deliberately, with the
    /// limitation recorded here rather than hidden.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty()
    {
        const int MaximumAllowedBlockedStreakTicks = 125;
        var worstStreakTicks = 0;
        var worstDiagnostics = string.Empty;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            // Sixteen agents per faction, every one of them inside
            // FormationRules.MaximumLastStandThresholdAgents — the
            // square-packing bound, which is 9 — so both factions are in last
            // stand from tick zero and are the most tightly clustered
            // configuration the design permits.
            var scenario = Scenario.CreateDefault(seed, totalAgents: 32) with
            {
                LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
            };
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            if (simulation.LongestBlockedStreakTicks <= worstStreakTicks)
            {
                continue;
            }

            var livingFaction0 = simulation.Agents.Count(
                agent => agent.FactionId == 0 && agent.IsAlive);
            var livingFaction1 = simulation.Agents.Count(
                agent => agent.FactionId == 1 && agent.IsAlive);
            worstStreakTicks = simulation.LongestBlockedStreakTicks;
            worstDiagnostics =
                $"seed {seed} stopped at tick {simulation.Tick} of " +
                $"{scenario.TickLimit}, outcome {simulation.Outcome}, " +
                $"living counts [{livingFaction0}, {livingFaction1}]";
        }

        Assert.True(
            worstStreakTicks <= MaximumAllowedBlockedStreakTicks,
            $"Longest observed blocked streak was {worstStreakTicks} ticks " +
            $"across seeds 1 to 20, exceeding the " +
            $"{MaximumAllowedBlockedStreakTicks}-tick PROVISIONAL bound. A " +
            "failure here means the last-stand cluster packs tighter than the " +
            "collision resolver permits (design risk R4). Worst seed: " +
            $"{worstDiagnostics}.");
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
    /// <remarks>
    /// <para>
    /// This swept twenty seeds until 2026-07-30 and was widened to two hundred
    /// because twenty cannot see what it was built to catch. The deadlock this
    /// guards against occurs on a low single-digit percentage of seeds: a
    /// 200-seed survey of the same configuration at neighbouring last-stand
    /// thresholds found 5 stalls in 200 at a threshold of 7 and 8 in 200 at a
    /// threshold of 8. A twenty-seed sweep has better-than-even odds of missing
    /// a defect at that rate entirely, so a pass proved close to nothing and the
    /// test was passing partly on luck.
    /// </para>
    /// <para>
    /// Two hundred is chosen because it is what the surveys behind
    /// the 2026-07-29 approach sidestep design ran at, so the
    /// bar this test holds and the evidence gathered against it are the same
    /// sample. Widening it is not free of information either: the residual
    /// stalls recorded in section 10 of that design are all at thresholds 7 and
    /// 8, and this test runs at the maximum, where 200 seeds are clean.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred()
    {
        const int TotalAgents = 18;
        const ulong LastSeed = 200;
        var stalledSeeds = new List<string>();

        for (ulong seed = 1; seed <= LastSeed; seed++)
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
            $"The following seeds of 1 to {LastSeed} never reached a terminal " +
            $"outcome before the tick limit:\n{string.Join('\n', stalledSeeds)}");
    }

    /// <summary>
    /// Regression lock for the approach sidestep, the pursuit-path counterpart
    /// of the rally stall escape. Each of these five seeds ran to the 10 000-tick
    /// limit before that escape existed and resolves normally with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seeds are not arbitrary. A 200-seed survey through
    /// <c>tools/Hukbo.Tools.DeadlockProbe</c> at a last-stand threshold of 8
    /// found eight stalling seeds; these are the five the sidestep clears. The
    /// remaining three — 5, 49 and 146 — still stall, for a reason recorded in
    /// section 10.1 of
    /// the 2026-07-29 approach sidestep design: their blocked
    /// warriors are enclosed rather than merely misdirected. Seed 49's longest
    /// blocked streak is 9 823 consecutive ticks, so the escape fires about
    /// fifty-one times and offers fifty-one different aim points without the
    /// warrior moving. Nothing the intent layer can choose frees a body that has
    /// no admissible step in any direction, so those three are deliberately not
    /// listed here. Adding them would make this Fact fail for something it does
    /// not test.
    /// </para>
    /// <para>
    /// A threshold of 8 is not the shipping default of 6, which was clean
    /// across all 200 seeds both before and after. It is used here because it is
    /// where the defect is reachable: the failing band sits between the shipping
    /// threshold and
    /// <see cref="FormationRules.MaximumLastStandThresholdAgents"/>, and no
    /// other test in this file exercises it.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(16UL)]
    [InlineData(44UL)]
    [InlineData(50UL)]
    [InlineData(125UL)]
    [InlineData(189UL)]
    public void APursuerBlockedByAComradeNoLongerHoldsTheBattleOpen(ulong seed)
    {
        const int TotalAgents = 18;
        const int LastStandThreshold = 8;

        var scenario = Scenario.CreateDefault(seed, totalAgents: TotalAgents) with
        {
            LastStandThresholdAgents = LastStandThreshold,
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
            simulation.Tick < scenario.TickLimit,
            $"Seed {seed} stalled at tick {simulation.Tick} of " +
            $"{scenario.TickLimit}, outcome {simulation.Outcome}, living " +
            $"counts [{livingFaction0}, {livingFaction1}], longest blocked " +
            $"streak {simulation.LongestBlockedStreakTicks} ticks. A failure " +
            "here means a pursuing warrior refused by a comrade's body can " +
            "again hold a whole battle open to the tick limit.");
    }

    /// <summary>
    /// Give-way regression coverage. Before this fix, a follower whose
    /// tick-start position happened to sit ahead of its own rally agent, on
    /// the rally agent's line of travel, aimed at a trail point behind the
    /// leader and had to walk backward through the leader's body to reach
    /// it. Solid collision then produced a head-on mutual block — the leader
    /// blocked forward by the follower, the follower blocked backward by the
    /// leader — that never cleared on its own.
    /// </summary>
    [Fact]
    public void AFollowerStandingInItsLeadersPathStepsAsideRatherThanThroughIt()
    {
        // Rally (2) and its enemy target (100) share a Y coordinate, so the
        // direction of travel is purely +X and every division below is
        // exact: no square-root rounding to account for.
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var rallyXRaw = checked(1000 * FixedPoint.Scale);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var enemyXRaw = checked(rallyXRaw + (200 * FixedPoint.Scale));

        // Ahead of the rally agent (relativeX > 0) and 600 raw units off the
        // axis — inside the give-way corridor half-width of
        // 2 * BodyRadiusRaw = 1024 raw units, but far enough past the
        // one-tick movement step (MovementSpeedRaw = 512) that a single
        // give-way step is guaranteed to carry it clear of the corridor.
        var followerXRaw = checked(rallyXRaw + 5_000);
        var followerYRaw = checked(rallyYRaw + 600);

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, factionId: 0, rallyXRaw, rallyYRaw, scenario),
            CreateAgentAtRawPosition(
                5, factionId: 0, followerXRaw, followerYRaw, scenario),
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, rallyYRaw, scenario));

        simulation.AdvanceOneTick();

        // Under the shipped default, PersistentContingentsV2, the give-way
        // aim point sits at a fixed distance of
        // corridorHalfWidthRaw + BodyRadiusRaw = 1536 raw units from the
        // follower's current position (see TryComputeGiveWayAimPoint), which
        // is inside the arrival taper band
        // (ArrivalTaperMultiplier * BodyRadiusRaw = 2048 raw units). The
        // very first give-way step is therefore deterministically capped at
        // Min(MovementSpeedRaw, 1536) * 1536 / 2048 = 384 raw units rather
        // than the full 512-unit step the comment above assumed when this
        // test predated the taper, so one tick alone no longer clears the
        // 1024-unit corridor. A second tick, whose aim point is again 1536
        // raw units from the follower's new position, reliably finishes the
        // escape, so the run advances twice before the corridor-clearance
        // check below.
        simulation.AdvanceOneTick();

        var after = AgentByEntityId(simulation, 5);
        Assert.Equal(AgentIntent.Regrouping, after.Intent);
        Assert.NotEqual(MovementResolution.Blocked, after.MovementResolution);

        // Forward projection unchanged: the give-way step has no component
        // along the leader's direction of travel (here, raw X), so X must
        // not move at all in this axis-aligned setup.
        Assert.Equal(followerXRaw, after.XRaw);

        // Left the corridor: the follower's distance from the leader's axis
        // (here, raw Y, since the axis is horizontal) must now be at or
        // beyond the corridor half-width, and it must have grown, not
        // shrunk or reversed sign — proof the step went further to the side
        // it was already on rather than across the leader's path.
        var corridorHalfWidthRaw = FormationRules.ComputeRallyCorridorHalfWidthRaw(
            scenario.BodyRadiusRaw);
        var lateralBefore = followerYRaw - rallyYRaw;
        var lateralAfter = after.YRaw - rallyYRaw;
        Assert.True(
            lateralAfter > lateralBefore,
            $"Expected the follower to move further from the leader's axis " +
            $"(lateral before {lateralBefore}, after {lateralAfter}).");
        Assert.True(
            Math.Abs(lateralAfter) >= corridorHalfWidthRaw,
            $"Expected the follower to have left the give-way corridor " +
            $"(half-width {corridorHalfWidthRaw}), but its lateral offset " +
            $"was only {Math.Abs(lateralAfter)}.");
    }

    /// <summary>
    /// A follower that is ahead of its rally agent along the direction of
    /// travel, but far enough off to the side to sit outside the give-way
    /// corridor, must still use the ordinary trail-plus-jitter aim point —
    /// proof the corridor test does not fire merely because a follower is
    /// ahead.
    /// </summary>
    [Fact]
    public void AFollowerClearOfTheCorridorStillTrailsBehindTheLeader()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var rallyXRaw = checked(1000 * FixedPoint.Scale);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var enemyXRaw = checked(rallyXRaw + (200 * FixedPoint.Scale));

        // Ahead of the rally agent (5 world units) and 10 world units off
        // the axis — well outside the 2 * BodyRadiusRaw = 1024 raw unit
        // (~1 world unit) corridor half-width.
        var followerXRaw = checked(rallyXRaw + (5 * FixedPoint.Scale));
        var followerYRaw = checked(rallyYRaw + (10 * FixedPoint.Scale));

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, factionId: 0, rallyXRaw, rallyYRaw, scenario),
            CreateAgentAtRawPosition(
                5, factionId: 0, followerXRaw, followerYRaw, scenario),
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, rallyYRaw, scenario));

        simulation.AdvanceOneTick();

        var after = AgentByEntityId(simulation, 5);
        Assert.Equal(AgentIntent.Regrouping, after.Intent);

        // The trail-plus-jitter aim point sits behind the rally agent
        // (RallyTrailRadiusMultiplier = 12 body radii always exceeds the
        // maximum jitter magnitude), so a follower using it moves backward
        // in X — the opposite of what a (wrongly triggered) sideways
        // give-way step would do, which would leave X exactly unchanged.
        Assert.True(
            after.XRaw < followerXRaw,
            "Expected the follower to move toward the trail point behind " +
            $"the rally agent (X should decrease from {followerXRaw}), " +
            $"but X was {after.XRaw}.");

        // The jitter offset is small relative to the follower's 10-world-unit
        // lateral displacement, so the aim point's Y sits close to the rally
        // agent's own Y — the follower must move toward it, not further
        // away, which is what a (wrongly triggered) give-way step would do
        // in this same-sign configuration.
        Assert.True(
            after.YRaw < followerYRaw,
            "Expected the follower to move toward the rally agent's Y, not " +
            $"further away from it. Y was {followerYRaw}, became {after.YRaw}.");
    }

    /// <summary>
    /// A follower sitting exactly on the rally agent's own axis of travel
    /// (lateral offset of zero) must break the give-way tie the same way no
    /// matter what order the agents were supplied in — otherwise the escape
    /// side, and therefore the resulting state hash, would depend on
    /// incidental array order.
    /// </summary>
    [Fact]
    public void TheGiveWaySideIsStableWhenAFollowerIsExactlyOnTheLeadersAxis()
    {
        var scenario = CreateTestScenario(lastStandThreshold: 2);
        var rallyXRaw = checked(1000 * FixedPoint.Scale);
        var rallyYRaw = checked(1000 * FixedPoint.Scale);
        var enemyXRaw = checked(rallyXRaw + (200 * FixedPoint.Scale));
        var followerXRaw = checked(rallyXRaw + 5_000);
        var followerYRaw = rallyYRaw;

        AgentState Rally() =>
            CreateAgentAtRawPosition(2, factionId: 0, rallyXRaw, rallyYRaw, scenario);
        AgentState Follower() =>
            CreateAgentAtRawPosition(
                5, factionId: 0, followerXRaw, followerYRaw, scenario);
        AgentState Enemy() =>
            CreateAgentAtRawPosition(100, factionId: 1, enemyXRaw, rallyYRaw, scenario);

        var orderingA = BattleSimulation.CreateForTesting(
            scenario, Rally(), Follower(), Enemy());
        var orderingB = BattleSimulation.CreateForTesting(
            scenario, Enemy(), Follower(), Rally());
        var orderingC = BattleSimulation.CreateForTesting(
            scenario, Follower(), Enemy(), Rally());

        orderingA.AdvanceOneTick();
        orderingB.AdvanceOneTick();
        orderingC.AdvanceOneTick();

        var afterA = AgentByEntityId(orderingA, 5);
        var afterB = AgentByEntityId(orderingB, 5);
        var afterC = AgentByEntityId(orderingC, 5);

        Assert.Equal((afterA.XRaw, afterA.YRaw), (afterB.XRaw, afterB.YRaw));
        Assert.Equal((afterA.XRaw, afterA.YRaw), (afterC.XRaw, afterC.YRaw));
        Assert.Equal(orderingA.ComputeStateHash(), orderingB.ComputeStateHash());
        Assert.Equal(orderingA.ComputeStateHash(), orderingC.ComputeStateHash());

        // Ties break toward the fixed "+" perpendicular. For this
        // purely-+X direction of travel that perpendicular is raw -Y, so
        // the follower's Y must move below the axis (never above it and
        // never stay put), regardless of array order.
        Assert.True(
            afterA.YRaw < followerYRaw,
            $"Expected the tie-break to move the follower's Y below the " +
            $"axis ({followerYRaw}), but it was {afterA.YRaw}.");
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
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));

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

    /// <summary>
    /// The movement preset is named rather than inherited from
    /// <see cref="Scenario"/>'s own default. Every assertion in this file
    /// describes the unconditional trail-plus-jitter behaviour, which
    /// <see cref="MovementPresetId.LastStandEngagementV11"/> changes: a
    /// follower under that preset stops regrouping once its rally agent is
    /// engaged or its own enemy is inside its own reach. These tests freeze the
    /// input they were written against instead of tracking whatever the shipped
    /// default becomes, and the new behaviour has its own suite in
    /// <c>LastStandEngagementV11Tests</c>. Naming V4 here changes nothing
    /// today — it is what the default already resolved to — and stops a later
    /// default flip from silently rewriting what this file asserts.
    /// </summary>
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
            MovementPreset = MovementPresetId.PersistentContingentsV4,
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
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));
}
