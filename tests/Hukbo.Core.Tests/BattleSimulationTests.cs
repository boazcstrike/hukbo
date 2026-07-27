using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class BattleSimulationTests
{
    [Fact]
    public void AgentIntentNumericValuesArePinned()
    {
        Assert.Equal(0, (int)AgentIntent.Idle);
        Assert.Equal(1, (int)AgentIntent.Moving);
        Assert.Equal(2, (int)AgentIntent.Attacking);
        Assert.Equal(3, (int)AgentIntent.Dead);
        Assert.Equal(4, (int)AgentIntent.Regrouping);
        Assert.Equal(5, Enum.GetValues<AgentIntent>().Length);
    }

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
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 100, y: 10, scenario));

        simulation.AdvanceOneTick();

        var mover = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 1);
        Assert.Equal((10 * FixedPoint.Scale) + (FixedPoint.Scale / 2), mover.XRaw);
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

        // Placed already in body contact, one world unit apart against a
        // half-unit radius. Agents now close to contact rather than halting at
        // reach, so a pair starting at reach would still be advancing on the
        // second tick and the feed would not be quiet. Starting in contact is
        // what makes the second tick genuinely empty, which is the condition
        // this test needs in order to observe the retained snapshot.
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 11, y: 10, scenario));

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
    public void RepeatedCollisionTicksHaveBoundedAllocations()
    {
        const int measuredTicks = 1_000;

        // Raised from 500,000 when agents began closing to body contact instead
        // of halting at reach: the crowd now jostles every tick, so far more
        // Move events are emitted. This ceiling tracks event traffic, which the
        // collision stage does not own. The window comparison below is the
        // assertion that actually guards collision storage.
        const long maximumAllocatedBytes = 900_000;
        const int agentsPerFaction = 12;

        // Crowd two lines into one another so the resolver works every tick:
        // the grid rebuilds, pairs are generated, and the movers behind the
        // front are blocked or truncated instead of walking freely. Hit points
        // are high and damage minimal so nobody dies inside the measured
        // window, which keeps the crowd intact for the whole run.
        // Two measured windows plus warm-up must all fit inside the limit, or
        // the battle ends in a draw part-way and the second window measures
        // no-op ticks.
        var scenario = CreateTestScenario() with
        {
            TickLimit = (measuredTicks * 2) + 100,
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackCooldownTicks = 20,
        };

        var agents = new List<AgentState>(agentsPerFaction * 2);
        for (var index = 0; index < agentsPerFaction; index++)
        {
            agents.Add(
                CreateAgent(
                    checked((ulong)index + 1),
                    factionId: 0,
                    x: 90 - (index % 3),
                    y: 40 + index,
                    scenario));
            agents.Add(
                CreateAgent(
                    checked((ulong)(agentsPerFaction + index) + 1),
                    factionId: 1,
                    x: 110 + (index % 3),
                    y: 40 + index,
                    scenario));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            [.. agents]);

        for (var tick = 0; tick < 32; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var firstWindowStart = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var secondWindowStart = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var windowEnd = GC.GetAllocatedBytesForCurrentThread();
        var firstWindowBytes = secondWindowStart - firstWindowStart;
        var secondWindowBytes = windowEnd - secondWindowStart;

        Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome);

        // The ceiling is generous because per-tick event traffic dominates it:
        // twenty-four agents in sustained contact emit far more events than the
        // two-agent quiet scenario above, and each tick's event list is an
        // allocation the collision stage does not control.
        Assert.True(
            firstWindowBytes <= maximumAllocatedBytes,
            $"Collision ticks allocated {firstWindowBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");

        // This is the assertion that actually guards the collision buffers.
        // Grid cells, pair lists, proposal buffers, and resolver scratch are all
        // reused, so a second identical window must not cost more than the
        // first. Any growth means something is reallocating per tick.
        Assert.True(
            secondWindowBytes <= firstWindowBytes,
            $"A warm window allocated {secondWindowBytes:N0} bytes after a " +
            $"first window of {firstWindowBytes:N0}. Collision storage must " +
            "be reused, growing only when capacity is insufficient.");
    }

    [Fact]
    public void CollisionTicksActuallyExerciseTheResolver()
    {
        // Guards the allocation test above: a crowd that never blocks anyone
        // would keep that budget trivially, and the measurement would prove
        // nothing about the collision stage.
        const int agentsPerFaction = 12;
        var scenario = CreateTestScenario() with
        {
            TickLimit = 500,
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackCooldownTicks = 20,
        };

        var agents = new List<AgentState>(agentsPerFaction * 2);
        for (var index = 0; index < agentsPerFaction; index++)
        {
            agents.Add(
                CreateAgent(
                    checked((ulong)index + 1),
                    factionId: 0,
                    x: 90 - (index % 3),
                    y: 40 + index,
                    scenario));
            agents.Add(
                CreateAgent(
                    checked((ulong)(agentsPerFaction + index) + 1),
                    factionId: 1,
                    x: 110 + (index % 3),
                    y: 40 + index,
                    scenario));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            [.. agents]);

        var constrained = false;
        for (var tick = 0; tick < 200 && !constrained; tick++)
        {
            simulation.AdvanceOneTick();
            constrained = simulation.Agents.Any(
                agent => agent.MovementResolution
                    is MovementResolution.Blocked
                    or MovementResolution.Truncated
                    or MovementResolution.Slid);
        }

        Assert.True(
            constrained,
            "No agent was ever blocked, truncated, or slid, so the allocation " +
            "measurement would not be exercising the collision resolver.");
    }

    /// <summary>
    /// Neither faction may hold a standing advantage across seeds. This asserts
    /// a distribution rather than mere presence: it previously required only one
    /// victory each, and passed on exactly one seed while the collision stage
    /// was handing faction 0 every contested push of every battle. Four in
    /// twenty is loose enough that ordinary seed variance cannot fail it and
    /// tight enough that a returning structural bias would.
    /// </summary>
    [Fact]
    public void SeedsOneThroughTwentyProduceVictoriesForBothFactions()
    {
        const int minimumVictoriesPerFaction = 4;
        var faction0Victories = 0;
        var faction1Victories = 0;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: 200);
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing)
            {
                simulation.AdvanceOneTick();
            }

            switch (simulation.Outcome)
            {
                case BattleOutcome.Faction0Victory:
                    faction0Victories++;
                    break;

                case BattleOutcome.Faction1Victory:
                    faction1Victories++;
                    break;

                case BattleOutcome.Ongoing:
                case BattleOutcome.Draw:
                default:
                    break;
            }
        }

        Assert.True(
            faction0Victories >= minimumVictoriesPerFaction &&
            faction1Victories >= minimumVictoriesPerFaction,
            $"Faction 0 won {faction0Victories} of 20 seeds and faction 1 won " +
            $"{faction1Victories}. Each faction must win at least " +
            $"{minimumVictoriesPerFaction}.");
    }

    /// <summary>
    /// Acceptance row <c>Battle completion</c> of
    /// <c>docs/plans/2026-07-27-formation-collision-mechanics.md</c>: the
    /// canonical two-hundred-agent battle still reaches a decisive result well
    /// inside its tick limit. Solid bodies must not turn the battle into a
    /// stalemate that only the limit ends.
    /// </summary>
    [Fact]
    public void CanonicalTwoHundredAgentBattleTerminatesWithinTheTickLimit()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200);
        var simulation = BattleSimulation.Create(scenario);

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Tick < scenario.TickLimit,
            $"The canonical battle reached tick {simulation.Tick} of a " +
            $"{scenario.TickLimit} tick limit without resolving.");
        Assert.Contains(
            simulation.Outcome,
            new[] { BattleOutcome.Faction0Victory, BattleOutcome.Faction1Victory });
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
            // Bodies are half a world unit across so that the hand-placed agents
            // below stay clear of one another, and the step is capped at the
            // radius by the tunneling guard in Scenario.Validate.
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
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

    [Fact]
    public void EntitiesOneThroughFourReceiveTheConfiguredRosterInOrder()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 8);
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        Assert.All(
            simulation.Agents,
            agent => Assert.Equal(
                rules.ResolveLoadout(agent.EntityId),
                agent.Loadout));
    }

    [Fact]
    public void TheEntityAfterTheLastRosterEntryWrapsToTheFirstLoadout()
    {
        // Derived from the roster length rather than hard-coded, so growing
        // the roster retunes the test instead of breaking it. Round-robin
        // assignment is (entityId - 1) % rosterCount, so the first wrap lands
        // on entity rosterCount + 1.
        var scenario = Scenario.CreateDefault(totalAgents: 20);
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var firstWrappedEntityId = (ulong)rules.Roster.Count + 1;

        var entityOne = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 1);
        var wrapped = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == firstWrappedEntityId);

        Assert.Equal(entityOne.Loadout, wrapped.Loadout);
        Assert.Equal(rules.Roster[0], wrapped.Loadout);
    }

    [Fact]
    public void BothFactionsAssignLoadoutsUsingTheSameEntityIdRule()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 8);
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        var faction0 = simulation.Agents.Where(agent => agent.FactionId == 0);
        var faction1 = simulation.Agents.Where(agent => agent.FactionId == 1);

        Assert.NotEmpty(faction0);
        Assert.NotEmpty(faction1);
        Assert.All(
            faction0.Concat(faction1),
            agent => Assert.Equal(
                rules.ResolveLoadout(agent.EntityId),
                agent.Loadout));
    }

    [Fact]
    public void RepeatedCreationOfTheSameScenarioProducesIdenticalLoadouts()
    {
        var scenario = Scenario.CreateDefault(seed: 7, totalAgents: 8);

        var first = BattleSimulation.Create(scenario);
        var second = BattleSimulation.Create(scenario);

        Assert.Equal(
            first.Agents.Select(agent => agent.Loadout),
            second.Agents.Select(agent => agent.Loadout));
    }

    [Fact]
    public void CreateUsesRoundRobinLoadoutsWhenRosterCountsAreEmpty()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 8) with
        {
            RosterCounts = ImmutableArray<int>.Empty,
        };
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        Assert.All(
            simulation.Agents,
            agent => Assert.Equal(
                rules.ResolveLoadout(agent.EntityId),
                agent.Loadout));
    }

    [Fact]
    public void CreateAssignsLoadoutsByFactionLocalIndexWhenRosterCountsAreProvided()
    {
        // AgentsPerFaction (6) is not a multiple of the four-entry roster,
        // so the unmodified round-robin path would misalign against this
        // faction-local expansion; a passing test here proves the new
        // branch, not a coincidence of the numbers chosen.
        var scenario = Scenario.CreateDefault(totalAgents: 12) with
        {
            RosterCounts = ImmutableArray.Create(2, 2, 1, 1, 0, 0),
        };
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var expectedRosterIndices = new[] { 0, 0, 1, 1, 2, 3 };

        var faction0 = simulation.Agents
            .Where(agent => agent.FactionId == 0)
            .OrderBy(agent => agent.EntityId)
            .ToArray();

        for (var localIndex = 0; localIndex < faction0.Length; localIndex++)
        {
            Assert.Equal(
                rules.Roster[expectedRosterIndices[localIndex]],
                faction0[localIndex].Loadout);
        }
    }

    [Fact]
    public void BothFactionsReceiveTheSameCategoryAtTheSameFactionLocalIndex()
    {
        // Same non-multiple AgentsPerFaction as above: the unmodified
        // round-robin path continues faction 1's entity IDs from faction
        // 0's, so it would give the two factions different armies here.
        var scenario = Scenario.CreateDefault(totalAgents: 12) with
        {
            RosterCounts = ImmutableArray.Create(2, 2, 1, 1, 0, 0),
        };
        var simulation = BattleSimulation.Create(scenario);

        var faction0 = simulation.Agents
            .Where(agent => agent.FactionId == 0)
            .OrderBy(agent => agent.EntityId)
            .Select(agent => agent.Loadout)
            .ToArray();
        var faction1 = simulation.Agents
            .Where(agent => agent.FactionId == 1)
            .OrderBy(agent => agent.EntityId)
            .Select(agent => agent.Loadout)
            .ToArray();

        Assert.Equal(faction0, faction1);
    }

    [Fact]
    public void RosterCountsDoNotChangeTheRandomDrawSequenceForSpawnPositions()
    {
        var baseline = Scenario.CreateDefault(seed: 3, totalAgents: 8);
        var withComposition = baseline with
        {
            RosterCounts = ImmutableArray.Create(1, 1, 1, 1, 0, 0),
        };

        var baselineSimulation = BattleSimulation.Create(baseline);
        var compositionSimulation = BattleSimulation.Create(withComposition);

        var baselinePositions = baselineSimulation.Agents
            .OrderBy(agent => agent.EntityId)
            .Select(agent => (agent.XRaw, agent.YRaw))
            .ToArray();
        var compositionPositions = compositionSimulation.Agents
            .OrderBy(agent => agent.EntityId)
            .Select(agent => (agent.XRaw, agent.YRaw))
            .ToArray();

        Assert.Equal(baselinePositions, compositionPositions);
    }

    [Fact]
    public void AcceptedAttacksCarryTheSourceWeaponAndAResolvedHitLocation()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var attackerLoadout = new CombatLoadout(
            WeaponId.Itak,
            ArmorId.LightOrganic,
            ShieldId.None);
        var defenderLoadout = new CombatLoadout(
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.TallHardwood);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario, attackerLoadout),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario, defenderLoadout));

        simulation.AdvanceOneTick();

        var attackFromOne = Assert.Single(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack &&
                battleEvent.SourceEntityId == 1);
        Assert.Equal(WeaponId.Itak, attackFromOne.Weapon);
        Assert.Equal(scenario.DamagePerAttack, attackFromOne.Value);
        Assert.True(
            attackFromOne.HitLocation is { } part && Enum.IsDefined(part));

        var expectedLocation = HitLocationResolver.Resolve(
            CombatPresetRegistry.Get(scenario.CombatPreset),
            attackerLoadout,
            defenderLoadout,
            scenario.Seed,
            simulation.Tick,
            sourceEntityId: 1,
            targetEntityId: 2);
        Assert.Equal(expectedLocation, attackFromOne.HitLocation);
    }

    [Fact]
    public void MultipleAttackersOnOneTargetRetainIndividualHitLocationsButOneAggregatedDamageEvent()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(
                1,
                factionId: 0,
                x: 10,
                y: 10,
                scenario,
                new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None)),
            CreateAgent(
                2,
                factionId: 0,
                x: 12,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.Wasay,
                    ArmorId.LightOrganic,
                    ShieldId.TallHardwood)),
            CreateAgent(
                3,
                factionId: 1,
                x: 11,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.Kalis,
                    ArmorId.LightOrganic,
                    ShieldId.None)));

        simulation.AdvanceOneTick();

        var attacksOnThree = simulation.LastEvents
            .Where(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack &&
                    battleEvent.TargetEntityId == 3)
            .ToArray();
        Assert.Equal(2, attacksOnThree.Length);
        Assert.Equal(
            [WeaponId.Wasay, WeaponId.Itak],
            attacksOnThree
                .Select(battleEvent => battleEvent.Weapon!.Value)
                .OrderBy(weapon => weapon));
        Assert.All(
            attacksOnThree,
            battleEvent => Assert.True(battleEvent.HitLocation.HasValue));

        var damageOnThree = Assert.Single(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Damage &&
                battleEvent.TargetEntityId == 3);
        Assert.Equal(2 * scenario.DamagePerAttack, damageOnThree.Value);

        var viewThree = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 3);
        Assert.Equal(
            scenario.MaximumHitPoints - (2 * scenario.DamagePerAttack),
            viewThree.HitPoints);
    }

    [Fact]
    public void NonAttackEventsNeverCarryWeaponOrHitLocation()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.NotEmpty(simulation.LastEvents);
        Assert.Contains(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack);
        Assert.All(
            simulation.LastEvents.Where(
                battleEvent => battleEvent.Kind != BattleEventKind.Attack),
            battleEvent =>
            {
                Assert.Null(battleEvent.Weapon);
                Assert.Null(battleEvent.HitLocation);
            });
        Assert.All(
            simulation.LastEvents.Where(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack),
            battleEvent =>
            {
                Assert.NotNull(battleEvent.Weapon);
                Assert.NotNull(battleEvent.HitLocation);
            });
    }
}
