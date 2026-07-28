using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
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

    /// <summary>
    /// The property under test is cooldown spacing, not interception, so the
    /// simulation runs on a zero-interception ruleset and the hit-point
    /// assertions keep meaning what they meant before the clash existed.
    /// </summary>
    /// <remarks>
    /// Design section 5 disposition. No shipped loadout pairing is
    /// clash-neutral — the minimum total interception is a
    /// <see cref="WeaponId.Wasay"/> defending a
    /// <see cref="WeaponId.Kalis"/> at 2,000 basis points, and every
    /// defender carries a non-zero void channel — so the seam is the only sound
    /// mechanism. Hand-picking a seed or an entity identifier whose roll happens
    /// to land would be silently invalidated by any later re-tune or mixer
    /// change, turning a preserved regression test into one that no longer tests
    /// what its name claims.
    /// </remarks>
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
            PresetWith(ClashProfile.Neutral),
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

    /// <summary>
    /// T8: pins the boundary the property doc on <see cref="BattleSimulation.LastEvents"/>
    /// actually guards. <see cref="LastEventsRemainsACompletedTickSnapshot"/>
    /// above proves a caller survives holding a reference across one quiet
    /// tick; this test proves that grace is an implementation detail of the
    /// double-buffer scheme (see the field comment above <c>_eventBufferA</c>
    /// in BattleSimulation.cs), not a guarantee, so no caller may assume a
    /// retained <see cref="BattleSimulation.LastEvents"/> value stays valid
    /// past the tick that produced it.
    /// </summary>
    /// <remarks>
    /// Two agents in continuous contact with a one-tick cooldown attack every
    /// tick, so ticks 1 through 3 are all active and the two backing buffers
    /// alternate: tick 1 exposes buffer A, tick 2 exposes buffer B (A is
    /// untouched so far -- this is the one-tick grace the sibling test
    /// checks), and tick 3 clears buffer A as the very first step of
    /// <c>AdvanceOneTick</c>, before it even knows whether tick 3 itself will
    /// produce events. That is the moment a caller who captured
    /// <c>LastEvents</c> at tick 1 and never re-read it gets betrayed: the
    /// object identity <c>retainedEvents</c> never changes, but its contents
    /// silently become tick 3's data. A naive "retain the reference once and
    /// trust it" implementation of any consumer would fail here, because
    /// <c>retainedEvents</c> no longer equals the tick-1 snapshot it was
    /// captured from -- it now equals whatever tick 3 produced, with nothing
    /// to signal the switch happened.
    /// </remarks>
    [Fact]
    public void RetainedLastEventsReferenceIsNotValidPastTheProducingTick()
    {
        var scenario = CreateTestScenario() with
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackCooldownTicks = 1,
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(ClashProfile.Neutral),
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 11, y: 10, scenario));

        simulation.AdvanceOneTick();
        var retainedEvents = simulation.LastEvents;
        var tickOneEvents = retainedEvents.ToArray();
        Assert.NotEmpty(tickOneEvents);

        // Tick 2 also produces events, so it writes into the OTHER backing
        // buffer and exposes that one instead. retainedEvents (a view over
        // the first buffer) is untouched -- this is the one-tick grace
        // period the sibling test above exercises.
        simulation.AdvanceOneTick();
        Assert.Equal(tickOneEvents, retainedEvents);

        // Tick 3 reuses the first buffer -- the very one retainedEvents is a
        // view over -- clearing and rewriting it. The reference a caller has
        // been holding since tick 1 now reflects tick 3.
        simulation.AdvanceOneTick();
        var tickThreeEvents = simulation.LastEvents.ToArray();
        Assert.NotEmpty(tickThreeEvents);
        Assert.NotEqual(tickOneEvents, retainedEvents);
        Assert.Equal(tickThreeEvents, retainedEvents);
    }

    /// <summary>
    /// The property under test is simultaneity, not interception: both blows
    /// have to land for the mutual death to be observable at all. Design section
    /// 5 disposition, resolved through the ruleset seam rather than by a lucky
    /// tuple.
    /// </summary>
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
            PresetWith(ClashProfile.Neutral),
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

        // Lowered from 300,000 by T7 of the arch-informed performance hardening
        // workstream. That ceiling was sized when every event-bearing tick
        // allocated a fresh List<BattleEvent>; the simulation now owns its
        // event buffers and reuses them, and this window measures exactly
        // 0 bytes on every run observed. A ceiling 300,000 times larger than
        // the thing it measures is a guard that can no longer fire, so it is
        // retuned here to the measured figure with room for runtime noise.
        const long maximumAllocatedBytes = 8_192;
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

        // History, kept because it explains why this number moved so far.
        // The ceiling was raised to 500,000 when agents began closing to body
        // contact instead of halting at reach, and then to 900,000 at T43,
        // when the merged BattleEvent packed Weapon, Shield, HitLocation, and
        // Resolution into one _combatContext int. On that tree this window
        // measured 815,312 bytes, and every one of those bytes was per-tick
        // event traffic that the collision stage did not own.
        //
        // T7 of the arch-informed performance hardening workstream removed
        // that traffic: the simulation now owns its event buffers and reuses
        // them across ticks, so no event-bearing tick allocates a list. Both
        // windows below now measure between 0 and 2,064 bytes over 1,000
        // ticks, observed across thirteen full-suite runs.
        //
        // That change also made the assertion this test used to carry ill
        // posed. It was "the second window must not exceed the first", which
        // was a sound guard while both windows were roughly 800,000 bytes of
        // simulation allocation. With both windows near zero, the comparison
        // ranks two numbers that are dominated by runtime infrastructure
        // rather than by the simulation -- the same deterministic work reports
        // a different byte count from run to run -- and it failed about one
        // full-suite run in three.
        //
        // It is replaced by three assertions rather than one, because no single
        // assertion covers what the old one did once noise entered the
        // measurement.
        //
        // The first two are an absolute ceiling on each window. The old
        // relative form would have accepted a first window of 899,999 bytes;
        // these accept at most 16,384 in either. Reinstating a per-tick event
        // list in this 24-agent scenario would allocate 24 * 2 * 72 = 3,456
        // bytes a tick, or 3,456,000 across a window, which is 210 times the
        // ceiling; even a single boxed enumerator per tick would allocate
        // roughly 46,000 across a window, nearly three times it.
        //
        // The third keeps the relative guard, with a tolerance sized from the
        // measured noise. It is needed because an absolute ceiling alone is
        // NOT strictly stronger than the old relative form, and it is worth
        // being exact about why: a regression that allocated, say, 500 bytes
        // in the first window and 12,000 in the second would have failed the
        // old zero-tolerance comparison and would pass a 16,384-byte ceiling.
        // Growth between two identical windows is a real signal and it is kept.
        //
        // The tolerance is not strictness thrown away, but it is a genuine
        // relaxation of the old assertion and is not pretended otherwise. Zero
        // tolerance is unachievable here: the same deterministic workload
        // reports different byte counts run to run. The largest run-to-run
        // increase observed across thirteen full-suite runs was 1,032 bytes,
        // and 4,096 is four times that.
        const long maximumAllocatedBytes = 16_384;
        const long warmWindowGrowthTolerance = 4_096;
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

        Assert.True(
            firstWindowBytes <= maximumAllocatedBytes,
            $"Collision ticks allocated {firstWindowBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");

        // This is the assertion that guards the reused storage. Grid cells,
        // pair lists, proposal buffers, resolver scratch, and now the event
        // buffers are all reused, so a second identical window must stay
        // inside the same ceiling. Anything reallocating per tick blows
        // through it by two orders of magnitude, as the note above works out.
        Assert.True(
            secondWindowBytes <= maximumAllocatedBytes,
            $"A warm window allocated {secondWindowBytes:N0} bytes after a " +
            $"first window of {firstWindowBytes:N0}; expected at most " +
            $"{maximumAllocatedBytes:N0}. Collision and event storage must be " +
            "reused, growing only when capacity is insufficient.");

        // The relative guard, kept because an absolute ceiling alone would let
        // a window that grew several thousand bytes relative to its
        // predecessor pass unnoticed.
        Assert.True(
            secondWindowBytes <= firstWindowBytes + warmWindowGrowthTolerance,
            $"A warm window allocated {secondWindowBytes:N0} bytes after a " +
            $"first window of {firstWindowBytes:N0}, a growth of " +
            $"{secondWindowBytes - firstWindowBytes:N0} bytes against a " +
            $"tolerance of {warmWindowGrowthTolerance:N0}. Two identical " +
            "windows must cost the same to within measurement noise.");
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
    /// Neither faction may hold a standing advantage across seeds, and the
    /// battle must reach a decision quickly enough to be worth watching. This
    /// carries two independent properties.
    ///
    /// The fairness clause asserts a distribution rather than mere presence: it
    /// previously required only one victory each, and passed on exactly one seed
    /// while the collision stage was handing faction 0 every contested push of
    /// every battle. Four in twenty is loose enough that ordinary seed variance
    /// cannot fail it and tight enough that a returning structural bias would.
    ///
    /// The termination clause is acceptance criterion two of the weapon-clash
    /// change: at least nineteen of twenty seeds decide before the tick cap, and
    /// the median decisive tick sits at or below half the cap.
    /// </summary>
    /// <remarks>
    /// The median clause is the one that can actually fail. A termination-rate
    /// clause alone passes happily while every battle finishes at ninety-eight
    /// per cent of the cap. Interception is a multiplier on a stall rather than
    /// its cause, so if this clause goes red the attack rate and the damage per
    /// landed blow are examined before the clash tables.
    /// </remarks>
    [Fact]
    public void SeedsOneThroughTwentyProduceVictoriesForBothFactions()
    {
        const int Seeds = 20;
        const int MinimumDecisiveSeeds = 19;
        const int MedianDecisiveTickLimit = 5_000;
        const int MinimumVictoriesPerFaction = 4;

        var faction0Victories = 0;
        var faction1Victories = 0;
        var decisiveTicks = new List<long>(Seeds);

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: 200);
            var simulation = BattleSimulation.Create(scenario);

            // Bounded. An unbounded loop turns a stall into a suite that hangs
            // with no diagnosis rather than a test that fails and names the
            // seed, which would defeat the whole point of the criterion.
            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
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

            if (simulation.Outcome is BattleOutcome.Faction0Victory or
                BattleOutcome.Faction1Victory or
                BattleOutcome.Draw)
            {
                decisiveTicks.Add(simulation.Tick);
            }
        }

        Assert.True(
            faction0Victories >= MinimumVictoriesPerFaction &&
            faction1Victories >= MinimumVictoriesPerFaction,
            $"Faction 0 won {faction0Victories} of 20 seeds and faction 1 won " +
            $"{faction1Victories}. Each faction must win at least " +
            $"{MinimumVictoriesPerFaction}.");

        Assert.True(
            decisiveTicks.Count >= MinimumDecisiveSeeds,
            $"Only {decisiveTicks.Count} of {Seeds} seeds decided before the " +
            $"tick cap; at least {MinimumDecisiveSeeds} are required.");

        var sorted = decisiveTicks.Order().ToArray();
        var median = sorted[sorted.Length / 2];
        Assert.True(
            median <= MedianDecisiveTickLimit,
            $"The median decisive tick was {median}, above the " +
            $"{MedianDecisiveTickLimit} tick clause. Interception multiplies a " +
            "stall rather than causing one, so examine the attack rate and the " +
            "damage per landed blow before the clash tables.");
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

    [Fact]
    public void Create_WithAnInjectedRulesetRejectsARosterThatDisagreesWithTheScenarioPreset()
    {
        // Scenario.Validate checks roster counts against the registry and is
        // deliberately left alone, so a differently rostered ruleset would have
        // the scenario validated against one roster while the simulation ran on
        // another. Both injecting factories refuse it.
        var scenario = CreateTestScenario();
        var mismatched = BuildRulesetWithASingleEntryRoster();

        Assert.NotEqual(
            CombatPresetRegistry.Get(scenario.CombatPreset).Roster,
            mismatched.Roster);
        Assert.Throws<ArgumentException>(
            () => BattleSimulation.Create(scenario, mismatched));
        Assert.Throws<ArgumentException>(
            () => BattleSimulation.CreateForTesting(
                scenario,
                mismatched,
                CreateAgent(1, factionId: 0, x: 10, y: 10, scenario)));
    }

    [Fact]
    public void Create_WithTheInjectedPresetRulesetMatchesTheRegistryPathExactly()
    {
        // The seam must move no value. A ruleset routed through the injection
        // overload produces the same agents, the same events, and the same
        // state hash as the registry path.
        //
        // The injected profile is the preset's own. While the preset carried
        // ClashProfile.Neutral the two were the same object's worth of data and
        // naming Neutral here read as "the preset's profile"; now that the
        // preset ships real tables, injecting Neutral would be injecting a
        // genuinely different configuration and the two paths would rightly
        // diverge. The property under test is the seam, not the profile, and it
        // is unchanged. The zero-interception control run keeps its own test:
        // ZeroInterceptionProfile_ReproducesThePreClashDigest is where a
        // neutral profile is required to reproduce the pre-clash stream.
        var scenario = Scenario.CreateDefault(seed: 7, totalAgents: 20);
        var registryRules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var injected = registryRules.WithClashProfile(registryRules.ClashProfile);

        var registryPath = BattleSimulation.Create(scenario);
        var injectedPath = BattleSimulation.Create(scenario, injected);

        for (var tick = 0; tick < 50; tick++)
        {
            registryPath.AdvanceOneTick();
            injectedPath.AdvanceOneTick();

            Assert.Equal(registryPath.LastEvents, injectedPath.LastEvents);
            Assert.Equal(
                registryPath.ComputeStateHash(),
                injectedPath.ComputeStateHash());
            Assert.Equal(
                registryPath.ComputeStateHash(),
                injectedPath.ComputeStateHash(injected.ContentHash));
        }
    }

    [Fact]
    public void CreateForTesting_WithTheInjectedPresetRulesetMatchesTheRegistryPathExactly()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
            // D2: the clash profile folds into the content hash only when one
            // was supplied. Preset V1 (CreateTestScenario's default) declares
            // none, so WithClashProfile(registryRules.ClashProfile) below would
            // hand it the Neutral fallback explicitly and turn an undeclared
            // profile into a declared one, moving the content hash the state
            // hash depends on and breaking the very equivalence this test
            // checks. Preset V2 already declares a profile, so round-tripping
            // it through WithClashProfile changes nothing.
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };
        // The preset's own profile, for the reason recorded on
        // Create_WithTheInjectedPresetRulesetMatchesTheRegistryPathExactly.
        var registryRules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var injected = registryRules.WithClashProfile(registryRules.ClashProfile);

        var registryPath = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));
        var injectedPath = BattleSimulation.CreateForTesting(
            scenario,
            injected,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        registryPath.AdvanceOneTick();
        injectedPath.AdvanceOneTick();

        Assert.Equal(registryPath.LastEvents, injectedPath.LastEvents);
        Assert.Equal(
            registryPath.ComputeStateHash(),
            injectedPath.ComputeStateHash());
    }

    /// <summary>
    /// PROVISIONAL gameplay-tuning comparison, not a historical claim. The
    /// research says the visible gap between a shielded and a shieldless warrior
    /// is the part to defend hardest, above any absolute interception figure.
    /// </summary>
    /// <remarks>
    /// Both defenders carry the same weapon and differ only in the shield, so
    /// the shield channel is the only thing that can separate them. Hit points
    /// are high and damage minimal so nobody dies inside the measured window and
    /// both defenders take the same number of accepted attacks.
    /// </remarks>
    [Fact]
    public void ShieldedDefenderTakesLessDamageThanUnshieldedAtTheSameSeed()
    {
        var shieldedDamage = 0;
        var unshieldedDamage = 0;

        for (ulong seed = 1; seed <= 10; seed++)
        {
            shieldedDamage += MeasureDamageTaken(seed, ShieldId.TallHardwood);
            unshieldedDamage += MeasureDamageTaken(seed, ShieldId.None);
        }

        Assert.True(
            shieldedDamage < unshieldedDamage,
            "PROVISIONAL band. Expected a tall hardwood shield to reduce damage " +
            $"taken, but the shielded defender took {shieldedDamage} against " +
            $"{unshieldedDamage} for the shieldless one.");
        Assert.True(
            shieldedDamage < unshieldedDamage * 9 / 10,
            "PROVISIONAL band. Expected a comfortable margin rather than a " +
            $"handful of rolls: {shieldedDamage} against {unshieldedDamage}.");
    }

    [Fact]
    public void NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(BuildAlwaysEvadedProfile()),
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        simulation.AdvanceOneTick();

        var attacks = simulation.LastEvents
            .Where(battleEvent => battleEvent.Kind == BattleEventKind.Attack)
            .ToArray();

        Assert.NotEmpty(attacks);
        Assert.All(
            attacks,
            attack =>
            {
                Assert.Equal(AttackResolution.Evaded, attack.Resolution);
                Assert.Equal(0, attack.Value);

                // The attack event still carries its combat context: the hit
                // location of a non-landed blow is the point it was aimed at.
                Assert.NotNull(attack.Weapon);
                Assert.NotNull(attack.HitLocation);
            });
        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Damage);
        Assert.All(
            simulation.Agents,
            agent => Assert.Equal(scenario.MaximumHitPoints, agent.HitPoints));
    }

    [Fact]
    public void NonLandedAttack_StillResetsTheAttackerCooldown()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
            AttackCooldownTicks = 2,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(BuildAlwaysEvadedProfile()),
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 22, y: 10, scenario));

        simulation.AdvanceOneTick();
        var firstTickAttacks = simulation.LastEvents
            .Where(battleEvent => battleEvent.Kind == BattleEventKind.Attack)
            .ToArray();

        Assert.NotEmpty(firstTickAttacks);
        Assert.All(
            firstTickAttacks,
            attack => Assert.Equal(AttackResolution.Evaded, attack.Resolution));

        simulation.AdvanceOneTick();

        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack);

        simulation.AdvanceOneTick();

        Assert.Contains(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack);
    }

    /// <summary>
    /// A damage event disappears only when <em>every</em> attack on a target is
    /// non-landed. Two attackers, one landing and one not, must leave exactly
    /// one damage event carrying exactly one blow of damage.
    /// </summary>
    [Fact]
    public void MixedResolutionsOnOneTarget_AggregateOnlyTheLandedDamage()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };

        // Entity 1 attacks with a Bolo, whose cell against a Kalis
        // defender is zero, so it always lands. Entity 2 attacks with a
        // Wasay, whose cell is the whole roll space at a hard share of
        // one, so it is always arrested.
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(BuildSplitResolutionProfile()),
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
                    ShieldId.None)),
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

        var landed = Assert.Single(
            attacksOnThree,
            attack => attack.SourceEntityId == 1);
        var arrested = Assert.Single(
            attacksOnThree,
            attack => attack.SourceEntityId == 2);
        Assert.Equal(AttackResolution.Landed, landed.Resolution);
        Assert.Equal(scenario.DamagePerAttack, landed.Value);
        Assert.Equal(AttackResolution.Parried, arrested.Resolution);
        Assert.Equal(0, arrested.Value);

        var damageOnThree = Assert.Single(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Damage &&
                battleEvent.TargetEntityId == 3);
        Assert.Equal(scenario.DamagePerAttack, damageOnThree.Value);

        var viewThree = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 3);
        Assert.Equal(
            scenario.MaximumHitPoints - scenario.DamagePerAttack,
            viewThree.HitPoints);
    }

    /// <summary>
    /// The storage order the caller happens to use cannot reach any resolution.
    /// Both entity identifiers are folded into the clash key, so two warriors
    /// who swap identifiers are <em>expected</em> to resolve differently; the
    /// property under test is that the same identifiers in a different array
    /// order cannot.
    /// </summary>
    [Fact]
    public void CrowdedTarget_ResolvesIdenticallyUnderEveryStorageOrder()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
            MaximumHitPoints = 1_000,
            DamagePerAttack = 1,
        };
        var rules = PresetWith(BuildShippedClashTables());

        var ascending = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildCrowdedRoster(scenario, reversed: false, interleaved: false));
        var descending = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildCrowdedRoster(scenario, reversed: true, interleaved: false));
        var interleaved = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildCrowdedRoster(scenario, reversed: false, interleaved: true));

        for (var tick = 0; tick < 40; tick++)
        {
            ascending.AdvanceOneTick();
            descending.AdvanceOneTick();
            interleaved.AdvanceOneTick();

            Assert.Equal(ascending.LastEvents, descending.LastEvents);
            Assert.Equal(ascending.LastEvents, interleaved.LastEvents);
            Assert.Equal(
                ascending.ComputeStateHash(),
                descending.ComputeStateHash());
            Assert.Equal(
                ascending.ComputeStateHash(),
                interleaved.ComputeStateHash());
        }

        Assert.Contains(
            ascending.Agents,
            agent => agent.HitPoints < scenario.MaximumHitPoints);
    }

    private static int MeasureDamageTaken(ulong seed, ShieldId defenderShield)
    {
        var scenario = CreateTestScenario() with
        {
            Seed = seed,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            MaximumHitPoints = 100_000,
            DamagePerAttack = 1,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(BuildShippedClashTables()),
            CreateAgent(
                1,
                factionId: 0,
                x: 10,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.None)),
            CreateAgent(
                2,
                factionId: 1,
                x: 22,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.Kalis,
                    ArmorId.LightOrganic,
                    defenderShield)));

        for (var tick = 0; tick < 200; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var defender = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 2);
        return scenario.MaximumHitPoints - defender.HitPoints;
    }

    private static AgentState[] BuildCrowdedRoster(
        Scenario scenario,
        bool reversed,
        bool interleaved)
    {
        var agents = new List<AgentState>
        {
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 0, x: 10, y: 12, scenario),
            CreateAgent(3, factionId: 0, x: 10, y: 14, scenario),
            CreateAgent(4, factionId: 1, x: 20, y: 10, scenario),
            CreateAgent(5, factionId: 1, x: 20, y: 12, scenario),
            CreateAgent(6, factionId: 1, x: 20, y: 14, scenario),
        };

        if (reversed)
        {
            agents.Reverse();
        }
        else if (interleaved)
        {
            agents = [.. agents.OrderBy(agent => agent.EntityId % 2)];
        }

        return [.. agents];
    }

    /// <summary>
    /// The registered preset carrying one explicit clash profile. Every clash
    /// value a simulation case depends on is written out in this file rather
    /// than read from <see cref="PhilippineCombatPreset"/>, so no case here can
    /// pass vacuously against a neutral preset or be re-tuned out from under.
    /// </summary>
    private static CombatRuleset PresetWith(ClashProfile profile) =>
        CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV1)
            .WithClashProfile(profile);

    /// <summary>
    /// Every accepted attack meets empty air: the void channel is the whole roll
    /// space, so the outcome does not depend on the roll at all.
    /// </summary>
    private static ClashProfile BuildAlwaysEvadedProfile()
    {
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var defender in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(defender, shield)] = ClashProfile.BasisPointScale;
                foreach (var attacker in weapons)
                {
                    matrix[(defender, shield, attacker)] = 0;
                }
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept: 0,
            voidChannel: voidChannel,
            hardShareBases: weapons.ToDictionary(weapon => weapon, _ => 0),
            hardShareMultipliers: weapons.ToDictionary(
                weapon => weapon,
                _ => ClashProfile.HardShareMultiplierScale),
            minimumHardShareBasisPoints: 0,
            maximumHardShareBasisPoints: ClashProfile.BasisPointScale,
            maximumInterceptionBasisPoints: ClashProfile.BasisPointScale);
    }

    /// <summary>
    /// A Wasay against a Kalis defender is always arrested;
    /// every other pairing always lands. Roll-independent on both sides, so the
    /// case does not rest on a lucky tuple that a later re-tune would move.
    /// </summary>
    private static ClashProfile BuildSplitResolutionProfile()
    {
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var defender in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(defender, shield)] = 0;
                foreach (var attacker in weapons)
                {
                    matrix[(defender, shield, attacker)] =
                        defender == WeaponId.Kalis &&
                        attacker == WeaponId.Wasay
                            ? ClashProfile.BasisPointScale
                            : 0;
                }
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept: 0,
            voidChannel: voidChannel,
            hardShareBases: weapons.ToDictionary(
                weapon => weapon,
                weapon => weapon == WeaponId.Wasay
                    ? ClashProfile.BasisPointScale
                    : 0),
            hardShareMultipliers: weapons.ToDictionary(
                weapon => weapon,
                _ => ClashProfile.HardShareMultiplierScale),
            minimumHardShareBasisPoints: 0,
            maximumHardShareBasisPoints: ClashProfile.BasisPointScale,
            maximumInterceptionBasisPoints: ClashProfile.BasisPointScale);
    }

    /// <summary>
    /// The design section 3.3 tables, written out here rather than read from the
    /// preset, which still carries <see cref="ClashProfile.Neutral"/> until the
    /// implementation phase populates it.
    /// </summary>
    /// <remarks>
    /// <b>PROVISIONAL.</b> Gameplay tuning values, not historical measurements.
    /// All sixteen weapon-intercept cells have no evidentiary confidence
    /// whatsoever.
    /// </remarks>
    private static ClashProfile BuildShippedClashTables()
    {
        var weaponInterceptByWeaponPair = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>
        {
            [(WeaponId.Kampilan, WeaponId.Kampilan)] = 2_200,
            [(WeaponId.Kampilan, WeaponId.Wasay)] = 1_900,
            [(WeaponId.Kampilan, WeaponId.Kalis)] = 1_600,
            [(WeaponId.Kampilan, WeaponId.Itak)] = 2_000,
            [(WeaponId.Wasay, WeaponId.Kampilan)] = 1_500,
            [(WeaponId.Wasay, WeaponId.Wasay)] = 1_300,
            [(WeaponId.Wasay, WeaponId.Kalis)] = 1_100,
            [(WeaponId.Wasay, WeaponId.Itak)] = 1_400,
            [(WeaponId.Kalis, WeaponId.Kampilan)] = 500,
            [(WeaponId.Kalis, WeaponId.Wasay)] = 400,
            [(WeaponId.Kalis, WeaponId.Kalis)] = 600,
            [(WeaponId.Kalis, WeaponId.Itak)] = 600,
            [(WeaponId.Itak, WeaponId.Kampilan)] = 400,
            [(WeaponId.Itak, WeaponId.Wasay)] = 300,
            [(WeaponId.Itak, WeaponId.Kalis)] = 500,
            [(WeaponId.Itak, WeaponId.Itak)] = 500,
        };

        var voidByWeapon = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 1_000,
            [WeaponId.Wasay] = 900,
            [WeaponId.Kalis] = 1_000,
            [WeaponId.Itak] = 1_100,
        };

        var weaponIntercept = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var shield in Enum.GetValues<ShieldId>())
        {
            foreach (var (pair, value) in weaponInterceptByWeaponPair)
            {
                weaponIntercept[(pair.Defender, shield, pair.Attacker)] = value;
            }

            foreach (var (weapon, value) in voidByWeapon)
            {
                voidChannel[(weapon, shield)] = value;
            }
        }

        return new ClashProfile(
            weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: new Dictionary<WeaponId, int>
            {
                [WeaponId.Kampilan] = 3_300,
                [WeaponId.Wasay] = 4_000,
                [WeaponId.Kalis] = 1_200,
                [WeaponId.Itak] = 1_800,
            },
            hardShareMultipliers: new Dictionary<WeaponId, int>
            {
                [WeaponId.Kampilan] = 1_150,
                [WeaponId.Wasay] = 1_050,
                [WeaponId.Kalis] = 750,
                [WeaponId.Itak] = 700,
            },
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// A structurally valid ruleset whose roster is one loadout, so it cannot
    /// agree with the registered preset roster.
    /// </summary>
    private static CombatRuleset BuildRulesetWithASingleEntryRoster()
    {
        var weights = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1))
            .ToArray();
        var multipliers = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1_000))
            .ToArray();
        var weightProfile = new TargetWeightProfile(weights);

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV1,
            version: 1,
            generalTargets: weightProfile,
            weaponTargets: new Dictionary<WeaponId, TargetWeightProfile>
            {
                [WeaponId.Kampilan] = weightProfile,
            },
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = new TargetWeightProfile(multipliers),
            },
            roster:
            [
                new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            ]);
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
            // This file's rulesets (PresetWith, BuildRulesetWithASingleEntryRoster)
            // are all built off PhilippineCombatPreset.Rules (V1)'s four-loadout
            // roster. Scenario.CombatPreset now defaults to V2's six-loadout
            // roster, so it has to be pinned back to V1 here or
            // BattleSimulation.AssertRosterMatchesRegisteredPreset rejects every
            // injected ruleset in this file as a roster mismatch.
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV1,
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
    public void EveryAgentsRankMatchesItsResolvedRosterEntrysRank()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 12) with
        {
            RosterCounts = ImmutableArray.Create(2, 2, 1, 1, 0, 0),
        };
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        Assert.All(
            simulation.Agents,
            agent => Assert.Equal(
                rules.ResolveLoadout(agent.EntityId).Rank,
                agent.Rank));
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

    /// <summary>
    /// A landed attack carries its weapon, its hit location, and a value equal
    /// to the configured damage. Design section 5 disposition: this pairing — a
    /// <see cref="WeaponId.Itak"/> attacker against a
    /// <see cref="WeaponId.Kampilan"/> defender carrying a
    /// <see cref="ShieldId.TallHardwood"/> shield — computes to 3,000 basis
    /// points of non-landed resolution under the shipped tables, so the value
    /// assertion would fail about one exchange in three without the seam.
    /// </summary>
    /// <remarks>
    /// The non-landed sibling design section 5 asks for is
    /// <see cref="NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent"/>, which
    /// asserts the same weapon and hit-location carriage against a value of
    /// zero. Keep the two together: they are one property split across the
    /// landed and non-landed halves of the same event contract.
    /// </remarks>
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
            PresetWith(ClashProfile.Neutral),
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

    /// <summary>
    /// The property under test is that two attackers keep two individual hit
    /// locations while the target receives one aggregated damage event.
    /// Aggregation sums only landed attacks once the clash resolves, so design
    /// section 5 gives this a zero-interception ruleset and the aggregate stays
    /// the full two blows.
    /// </summary>
    [Fact]
    public void MultipleAttackersOnOneTargetRetainIndividualHitLocationsButOneAggregatedDamageEvent()
    {
        var scenario = CreateTestScenario() with
        {
            AttackRangeRaw = 12 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            PresetWith(ClashProfile.Neutral),
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

    /// <summary>
    /// Leadership plan L3: <see cref="AgentView.IsLeader"/> is a per-tick,
    /// recomputed-from-scratch fact wired from
    /// <see cref="Movement.MovementRules.ScanContingentLeadersAndLivingCounts"/>
    /// through <c>BattleSimulation.UpdateViews</c>. Every registered
    /// <see cref="MovementPresetId"/> is exercised, including
    /// <see cref="MovementPresetId.IndependentPursuitV1"/>, whose contingent
    /// state machine never runs at all — that preset must show every agent
    /// with <see cref="AgentView.IsLeader"/> false for the whole battle, not
    /// merely absent from the per-contingent count, per the design's
    /// verified-not-assumed note that <c>_contingentLeaderEntityIds</c> stays
    /// at its all-zero constructor value under V1 and 0 is never a valid
    /// entity id.
    /// </summary>
    /// <remarks>
    /// Two separate assertions, not one, because the tick pipeline runs
    /// <c>ResolveContingentStates</c> (the leader scan) before
    /// <c>GatherAndCommitAttacks</c> (combat): a leader designated at the
    /// start of a tick can die to combat that same tick, and the view a test
    /// observes is captured after both, at <c>UpdateViews</c>. That tick's
    /// view can therefore show zero living leaders in a still-non-empty
    /// contingent — the fallen leader's own (dead) view still carries
    /// <see cref="AgentView.IsLeader"/> true until the next tick's scan
    /// reassigns it, exactly as this plan's L4 task names for the pawn
    /// marker. What must never happen, on any tick, is <em>two</em> living
    /// members of the same contingent both reading as leader — that would be
    /// a genuine double-selection defect, not a same-tick timing artifact —
    /// so that check runs at every sampled tick. The weaker,
    /// timing-tolerant check — every contingent that is ever non-empty
    /// eventually reads exactly one living leader at some sampled tick —
    /// runs once, over the union of all samples, so a same-tick succession
    /// gap does not fail the test.
    /// </remarks>
    [Fact]
    public void ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset()
    {
        foreach (var preset in Enum.GetValues<MovementPresetId>())
        {
            Assert.True(MovementPresetRegistry.IsRegistered(preset));

            var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
            {
                MovementPreset = preset,
                CombatPreset = CombatPresetId.PrecolonialPhilippinesV4,
            };
            scenario.Validate();
            var simulation = BattleSimulation.Create(scenario);

            var everNonEmptyContingents = new HashSet<(int FactionId, int ContingentId)>();
            var everHadExactlyOneLivingLeaderContingents =
                new HashSet<(int FactionId, int ContingentId)>();

            // No sample before the first AdvanceOneTick(): the leader scan is
            // part of the tick pipeline, so before any tick has run,
            // _contingentLeaderEntityIds still holds its all-zero
            // constructor value under every preset, contingent-aware or not.
            for (var tick = 0;
                tick < 400 && simulation.Outcome == BattleOutcome.Ongoing;
                tick++)
            {
                simulation.AdvanceOneTick();

                if (tick % 25 != 0)
                {
                    continue;
                }

                if (preset == MovementPresetId.IndependentPursuitV1)
                {
                    Assert.All(simulation.Agents, agent => Assert.False(agent.IsLeader));
                    continue;
                }

                SampleLeadershipCounts(
                    simulation.Agents,
                    everNonEmptyContingents,
                    everHadExactlyOneLivingLeaderContingents);
            }

            if (preset != MovementPresetId.IndependentPursuitV1)
            {
                Assert.NotEmpty(everNonEmptyContingents);
                Assert.Equal(everNonEmptyContingents, everHadExactlyOneLivingLeaderContingents);
            }
        }
    }

    /// <summary>
    /// Asserts the hard, always-true invariant (never two living leaders in
    /// one contingent, on any sampled tick) and records into the two
    /// caller-owned sets the softer, whole-run invariant described on
    /// <see cref="ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset"/>.
    /// </summary>
    private static void SampleLeadershipCounts(
        IReadOnlyList<AgentView> agents,
        HashSet<(int FactionId, int ContingentId)> everNonEmptyContingents,
        HashSet<(int FactionId, int ContingentId)> everHadExactlyOneLivingLeaderContingents)
    {
        var leaderCountsByContingent = new Dictionary<(int FactionId, int ContingentId), int>();
        foreach (var agent in agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var key = (agent.FactionId, agent.ContingentId);
            leaderCountsByContingent.TryGetValue(key, out var count);
            leaderCountsByContingent[key] = count + (agent.IsLeader ? 1 : 0);
        }

        foreach (var (key, count) in leaderCountsByContingent)
        {
            Assert.True(
                count <= 1,
                $"Contingent {key} had {count} simultaneous living leaders.");
            everNonEmptyContingents.Add(key);
            if (count == 1)
            {
                everHadExactlyOneLivingLeaderContingents.Add(key);
            }
        }
    }
}
