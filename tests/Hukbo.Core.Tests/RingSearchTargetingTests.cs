using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

/// <summary>
/// Whole-simulation proof that ring-search targeting is hash-neutral. Two
/// simulations of one scenario run side by side, one through the ring search
/// and one forced onto the brute-force scan through
/// <see cref="BattleSimulation.ForcesBruteForceTargeting"/>, and every tick
/// they must report the same state hash and the same event hash. Design:
/// <c>docs/plans/2026-11-21-ring-search-targeting-design.md</c> section 6.
/// </summary>
public sealed class RingSearchTargetingTests
{
    private const int DifferentialAgents = 300;

    private const int DifferentialTicks = 2_000;

    private const int SmallArmyAgents = 24;

    private const int SmallArmyTicks = 1_500;

    private const int LargeArmyAgents = 300;

    private const int LargeArmyTicks = 450;

    private const int ShortPerceptionAgents = 120;

    private const int ShortPerceptionTicks = 600;

    /// <summary>
    /// A fraction of the 1,280 unit map, so an agent perceives only a nearby
    /// enemy and the search's perception stop is what ends most queries.
    /// </summary>
    private const int ShortPerceptionRaw = 700 * FixedPoint.Scale;

    private const int RangedHeavyAgents = 120;

    private const int RangedHeavyTicks = 700;

    /// <summary>How many times likelier a ranged roster row is than a melee one.</summary>
    private const int RangedRosterWeight = 5;

    private const int AllocationAgents = 1_000;

    private const int AllocationWarmUpTicks = 64;

    private const int AllocationMeasuredTicks = 200;

    /// <summary>
    /// Every registered movement preset that does not derive local context.
    /// Those that do (V6, V7, V9) keep the brute-force scan by design, so
    /// there is no second implementation to compare against.
    /// </summary>
    public static TheoryData<MovementPresetId> PresetsThatUseTheRingSearch()
    {
        var data = new TheoryData<MovementPresetId>();
        foreach (var preset in Enum.GetValues<MovementPresetId>())
        {
            if (MovementPresetRegistry.IsRegistered(preset) &&
                !MovementPresetRegistry.Get(preset).UsesEquipmentRelativeFootwork)
            {
                data.Add(preset);
            }
        }

        return data;
    }

    public static TheoryData<ulong> Seeds() => new() { 1UL, 2UL, 3UL, 4UL };

    [Theory]
    [MemberData(nameof(PresetsThatUseTheRingSearch))]
    public void RingSearchAndBruteForce_ProduceIdenticalHashesOnEveryTick(
        MovementPresetId movementPreset)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: DifferentialAgents) with
        {
            TickLimit = DifferentialTicks,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
            MovementPreset = movementPreset,
        };

        AssertRingSearchMatchesBruteForce(scenario, DifferentialTicks, movementPreset.ToString());
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SmallArmies_ProduceIdenticalHashesOnEveryTick(ulong seed)
    {
        var scenario = CreateShippedScenario(seed, SmallArmyAgents, SmallArmyTicks);

        AssertRingSearchMatchesBruteForce(scenario, SmallArmyTicks, $"small army, seed {seed}");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void LargeArmies_ProduceIdenticalHashesOnEveryTick(ulong seed)
    {
        var scenario = CreateShippedScenario(seed, LargeArmyAgents, LargeArmyTicks);

        AssertRingSearchMatchesBruteForce(scenario, LargeArmyTicks, $"large army, seed {seed}");
    }

    /// <summary>
    /// With perception well under the map width, most queries end because the
    /// next ring lies beyond perception, the stop that the default 2,048 unit
    /// perception never reaches on a 1,280 unit map.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ShortPerception_ProducesIdenticalHashesOnEveryTick(ulong seed)
    {
        var scenario = CreateShippedScenario(seed, ShortPerceptionAgents, ShortPerceptionTicks) with
        {
            PerceptionRangeRaw = ShortPerceptionRaw,
        };

        var run = AssertRingSearchMatchesBruteForce(
            scenario,
            ShortPerceptionTicks,
            $"short perception, seed {seed}");

        Assert.True(run.TicksWithATarget > 0, "No agent ever perceived an enemy.");
    }

    /// <summary>
    /// A ranged actor keeps searching until it has both its nearest enemy and
    /// its nearest melee enemy, so an army of mostly archers and arquebusiers
    /// is the composition that walks the most rings.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void RangedHeavyArmies_ProduceIdenticalHashesOnEveryTick(ulong seed)
    {
        var scenario = CreateShippedScenario(seed, RangedHeavyAgents, RangedHeavyTicks);
        scenario = scenario with
        {
            RosterCounts = CreateRangedHeavyRosterCounts(
                scenario.CombatPreset,
                scenario.AgentsPerFaction),
        };

        var run = AssertRingSearchMatchesBruteForce(
            scenario,
            RangedHeavyTicks,
            $"ranged-heavy army, seed {seed}");

        Assert.True(run.AcceptedAttacks > 0, "No attack was ever accepted.");
    }

    /// <summary>
    /// The existing allocation windows run two agents, which cannot see a grid
    /// that grows with the army. This one runs a thousand under the shipped
    /// presets (combat V8, movement V14) and requires the ring search to add
    /// nothing once its arrays have grown to size.
    /// </summary>
    [Fact]
    public void WarmTicksAtOneThousandAgentsAllocateNothing()
    {
        // The first melee attack in a process (tick 76 here) allocates 792
        // bytes once inside GatherAndCommitAttacks, so a throwaway run primes
        // the process and the measured run does not depend on test order.
        var primer = CreateAllocationSimulation();
        for (var tick = 0; tick < AllocationWarmUpTicks + AllocationMeasuredTicks; tick++)
        {
            primer.AdvanceOneTick();
        }

        var simulations = new List<BattleSimulation>();

        WarmAllocationWindow.AssertAllocatesNothing(
            () =>
            {
                var simulation = CreateAllocationSimulation();
                simulations.Add(simulation);
                for (var tick = 0; tick < AllocationWarmUpTicks; tick++)
                {
                    simulation.AdvanceOneTick();
                }

                return () =>
                {
                    for (var tick = 0; tick < AllocationMeasuredTicks; tick++)
                    {
                        simulation.AdvanceOneTick();
                    }
                };
            },
            $"{AllocationMeasuredTicks} warm ticks at {AllocationAgents} agents");

        Assert.All(simulations, simulation => Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome));
    }

    private static Scenario CreateShippedScenario(ulong seed, int totalAgents, int ticks) =>
        Scenario.CreateDefault(seed, totalAgents) with
        {
            TickLimit = ticks,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
            MovementPreset = MovementPresetId.EvasiveFootworkV14,
        };

    /// <summary>
    /// Counts for every roster row of the scenario's combat preset, a ranged
    /// row weighted <see cref="RangedRosterWeight"/> times a melee one. The
    /// rounding remainder goes to the first ranged row so the counts sum to
    /// exactly <paramref name="agentsPerFaction"/>.
    /// </summary>
    private static ImmutableArray<int> CreateRangedHeavyRosterCounts(
        CombatPresetId combatPreset,
        int agentsPerFaction)
    {
        var rules = CombatPresetRegistry.Get(combatPreset);
        var isRanged = rules.Roster
            .Select(loadout => rules
                .ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
                .StandoffDistanceRaw != 0)
            .ToArray();
        var weights = isRanged.Select(ranged => ranged ? RangedRosterWeight : 1).ToArray();
        var totalWeight = weights.Sum();
        var counts = weights.Select(weight => agentsPerFaction * weight / totalWeight).ToArray();
        counts[Array.IndexOf(isRanged, true)] += agentsPerFaction - counts.Sum();
        return [.. counts];
    }

    /// <summary>
    /// Runs the scenario through the ring search and the forced brute-force
    /// scan side by side and asserts that the state hash and the event hash
    /// match after every tick. Returns what the run exercised, so a caller can
    /// reject a scenario that never engaged.
    /// </summary>
    private static RunSummary AssertRingSearchMatchesBruteForce(
        Scenario scenario,
        int ticks,
        string label)
    {
        scenario.Validate();
        var ringSearch = BattleSimulation.Create(scenario);
        var bruteForce = BattleSimulation.Create(scenario);
        bruteForce.ForcesBruteForceTargeting = true;
        var ringEventHash = Fnv1a.OffsetBasis;
        var bruteEventHash = Fnv1a.OffsetBasis;
        var ticksWithATarget = 0;
        var acceptedAttacks = 0L;

        for (var tick = 1;
            tick <= ticks && ringSearch.Outcome == BattleOutcome.Ongoing;
            tick++)
        {
            ringSearch.AdvanceOneTick();
            bruteForce.AdvanceOneTick();
            foreach (var battleEvent in ringSearch.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref ringEventHash, battleEvent);
            }

            foreach (var battleEvent in bruteForce.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref bruteEventHash, battleEvent);
            }

            Assert.True(
                ringSearch.ComputeStateHash() == bruteForce.ComputeStateHash(),
                $"{label}: state hash diverged at tick {tick}.");
            Assert.True(
                ringEventHash == bruteEventHash,
                $"{label}: event hash diverged at tick {tick}.");
            Assert.Equal(ringSearch.Outcome, bruteForce.Outcome);

            if (ringSearch.Agents.Any(agent => agent.TargetEntityId is not null))
            {
                ticksWithATarget++;
            }

            acceptedAttacks += ringSearch.LastTickCombat.AcceptedAttacks;
        }

        Assert.Equal(
            bruteForce.ComputeStateHash().ToString("X16", CultureInfo.InvariantCulture),
            ringSearch.ComputeStateHash().ToString("X16", CultureInfo.InvariantCulture));
        return new RunSummary(ticksWithATarget, acceptedAttacks);
    }

    private static BattleSimulation CreateAllocationSimulation()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: AllocationAgents) with
        {
            TickLimit = AllocationWarmUpTicks + AllocationMeasuredTicks + 100,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
            MovementPreset = MovementPresetId.EvasiveFootworkV14,
        };
        scenario.Validate();
        return BattleSimulation.Create(scenario);
    }

    private readonly record struct RunSummary(int TicksWithATarget, long AcceptedAttacks);
}
