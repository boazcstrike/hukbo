using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
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
        scenario.Validate();
        var ringSearch = BattleSimulation.Create(scenario);
        var bruteForce = BattleSimulation.Create(scenario);
        bruteForce.ForcesBruteForceTargeting = true;
        var ringEventHash = Fnv1a.OffsetBasis;
        var bruteEventHash = Fnv1a.OffsetBasis;

        for (var tick = 1;
            tick <= DifferentialTicks && ringSearch.Outcome == BattleOutcome.Ongoing;
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
                $"{movementPreset}: state hash diverged at tick {tick}.");
            Assert.True(
                ringEventHash == bruteEventHash,
                $"{movementPreset}: event hash diverged at tick {tick}.");
            Assert.Equal(ringSearch.Outcome, bruteForce.Outcome);
        }

        Assert.Equal(
            bruteForce.ComputeStateHash().ToString("X16", CultureInfo.InvariantCulture),
            ringSearch.ComputeStateHash().ToString("X16", CultureInfo.InvariantCulture));
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

        var simulation = CreateAllocationSimulation();

        for (var tick = 0; tick < AllocationWarmUpTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < AllocationMeasuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBytes =
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome);
        Assert.True(
            allocatedBytes == 0,
            $"{AllocationMeasuredTicks} warm ticks at {AllocationAgents} agents " +
            $"allocated {allocatedBytes:N0} bytes; expected none.");
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
}
