using Hukbo.Core.Combat;
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
