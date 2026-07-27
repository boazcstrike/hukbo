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
        //
        // Raised again from 900,000 when BattleEvent widened from 80 to 88 bytes
        // to carry the nullable attack resolution. The measured figure moved from
        // about 898,000 to 988,192, the same 9.9 per cent the whole-workload
        // allocation moved by, and 900,000 had left only a fifth of a per cent of
        // headroom. The new ceiling restores about eleven per cent so one more
        // field does not break it, without loosening what the test claims: that
        // collision ticks allocate a bounded amount rather than growing with time.
        const long maximumAllocatedBytes = 1_100_000;
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
        // The seam must move no value. A ruleset that is the preset except for
        // its clash profile produces the same agents, the same events, and the
        // same state hash as the registry path.
        var scenario = Scenario.CreateDefault(seed: 7, totalAgents: 20);
        var injected = CombatPresetRegistry
            .Get(scenario.CombatPreset)
            .WithClashProfile(ClashProfile.Neutral);

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
        };
        var injected = CombatPresetRegistry
            .Get(scenario.CombatPreset)
            .WithClashProfile(ClashProfile.Neutral);

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

        // Entity 1 attacks with a Bolo, whose cell against a ThrustingBlade
        // defender is zero, so it always lands. Entity 2 attacks with a
        // HeavyChopper, whose cell is the whole roll space at a hard share of
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
                new CombatLoadout(WeaponId.Bolo, ArmorId.LightOrganic, ShieldId.None)),
            CreateAgent(
                2,
                factionId: 0,
                x: 12,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.HeavyChopper,
                    ArmorId.LightOrganic,
                    ShieldId.None)),
            CreateAgent(
                3,
                factionId: 1,
                x: 11,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.ThrustingBlade,
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
                    WeaponId.GreatBlade,
                    ArmorId.LightOrganic,
                    ShieldId.None)),
            CreateAgent(
                2,
                factionId: 1,
                x: 22,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.ThrustingBlade,
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
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = 0;
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept: 0,
            voidChannel: weapons.ToDictionary(
                weapon => weapon,
                _ => ClashProfile.BasisPointScale),
            hardShareBases: weapons.ToDictionary(weapon => weapon, _ => 0),
            hardShareMultipliers: weapons.ToDictionary(
                weapon => weapon,
                _ => ClashProfile.HardShareMultiplierScale),
            minimumHardShareBasisPoints: 0,
            maximumHardShareBasisPoints: ClashProfile.BasisPointScale,
            maximumInterceptionBasisPoints: ClashProfile.BasisPointScale);
    }

    /// <summary>
    /// A HeavyChopper against a ThrustingBlade defender is always arrested;
    /// every other pairing always lands. Roll-independent on both sides, so the
    /// case does not rest on a lucky tuple that a later re-tune would move.
    /// </summary>
    private static ClashProfile BuildSplitResolutionProfile()
    {
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] =
                    defender == WeaponId.ThrustingBlade &&
                    attacker == WeaponId.HeavyChopper
                        ? ClashProfile.BasisPointScale
                        : 0;
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept: 0,
            voidChannel: weapons.ToDictionary(weapon => weapon, _ => 0),
            hardShareBases: weapons.ToDictionary(
                weapon => weapon,
                weapon => weapon == WeaponId.HeavyChopper
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
    private static ClashProfile BuildShippedClashTables() =>
        new(
            new Dictionary<(WeaponId Defender, WeaponId Attacker), int>
            {
                [(WeaponId.GreatBlade, WeaponId.GreatBlade)] = 2_200,
                [(WeaponId.GreatBlade, WeaponId.HeavyChopper)] = 1_900,
                [(WeaponId.GreatBlade, WeaponId.ThrustingBlade)] = 1_600,
                [(WeaponId.GreatBlade, WeaponId.Bolo)] = 2_000,
                [(WeaponId.HeavyChopper, WeaponId.GreatBlade)] = 1_500,
                [(WeaponId.HeavyChopper, WeaponId.HeavyChopper)] = 1_300,
                [(WeaponId.HeavyChopper, WeaponId.ThrustingBlade)] = 1_100,
                [(WeaponId.HeavyChopper, WeaponId.Bolo)] = 1_400,
                [(WeaponId.ThrustingBlade, WeaponId.GreatBlade)] = 500,
                [(WeaponId.ThrustingBlade, WeaponId.HeavyChopper)] = 400,
                [(WeaponId.ThrustingBlade, WeaponId.ThrustingBlade)] = 600,
                [(WeaponId.ThrustingBlade, WeaponId.Bolo)] = 600,
                [(WeaponId.Bolo, WeaponId.GreatBlade)] = 400,
                [(WeaponId.Bolo, WeaponId.HeavyChopper)] = 300,
                [(WeaponId.Bolo, WeaponId.ThrustingBlade)] = 500,
                [(WeaponId.Bolo, WeaponId.Bolo)] = 500,
            },
            shieldIntercept: 2_400,
            voidChannel: new Dictionary<WeaponId, int>
            {
                [WeaponId.GreatBlade] = 1_000,
                [WeaponId.HeavyChopper] = 900,
                [WeaponId.ThrustingBlade] = 1_000,
                [WeaponId.Bolo] = 1_100,
            },
            hardShareBases: new Dictionary<WeaponId, int>
            {
                [WeaponId.GreatBlade] = 3_300,
                [WeaponId.HeavyChopper] = 4_000,
                [WeaponId.ThrustingBlade] = 1_200,
                [WeaponId.Bolo] = 1_800,
            },
            hardShareMultipliers: new Dictionary<WeaponId, int>
            {
                [WeaponId.GreatBlade] = 1_150,
                [WeaponId.HeavyChopper] = 1_050,
                [WeaponId.ThrustingBlade] = 750,
                [WeaponId.Bolo] = 700,
            },
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);

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
                [WeaponId.GreatBlade] = weightProfile,
            },
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = new TargetWeightProfile(multipliers),
            },
            roster:
            [
                new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None),
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
    public void EntityFiveWrapsToTheFirstRosterLoadout()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 10);
        var simulation = BattleSimulation.Create(scenario);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        var entityOne = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 1);
        var entityFive = Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == 5);

        Assert.Equal(entityOne.Loadout, entityFive.Loadout);
        Assert.Equal(rules.Roster[0], entityFive.Loadout);
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
            RosterCounts = ImmutableArray.Create(2, 2, 1, 1),
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
            RosterCounts = ImmutableArray.Create(2, 2, 1, 1),
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
            RosterCounts = ImmutableArray.Create(1, 1, 1, 1),
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
            WeaponId.Bolo,
            ArmorId.LightOrganic,
            ShieldId.None);
        var defenderLoadout = new CombatLoadout(
            WeaponId.GreatBlade,
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
        Assert.Equal(WeaponId.Bolo, attackFromOne.Weapon);
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
                new CombatLoadout(WeaponId.Bolo, ArmorId.LightOrganic, ShieldId.None)),
            CreateAgent(
                2,
                factionId: 0,
                x: 12,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.HeavyChopper,
                    ArmorId.LightOrganic,
                    ShieldId.TallHardwood)),
            CreateAgent(
                3,
                factionId: 1,
                x: 11,
                y: 10,
                scenario,
                new CombatLoadout(
                    WeaponId.ThrustingBlade,
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
            [WeaponId.HeavyChopper, WeaponId.Bolo],
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
