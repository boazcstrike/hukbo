using System.Globalization;
using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

/// <summary>
/// Integration tests for the separating-contact preset
/// <see cref="MovementPresetId.SeparatingContactV17"/>, wired into the tick by
/// <c>docs/plans/2026-11-21-wmao-optimizations.md</c> row SS-3 under
/// <c>docs/decisions/2026-11-21-separating-contact.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The bounded-overlap invariant is checked by brute force over every pair of
/// living agents after every tick, deliberately not through
/// <c>CollisionUniformGrid</c>, for the reason
/// <see cref="CollisionRegressionTests"/> gives: a broad-phase defect must not
/// be able to hide the overlap the invariant exists to catch.
/// </para>
/// <para>
/// Every separation constant is a provisional gameplay value read from the
/// registered ruleset, so the bound here follows the ruleset rather than a
/// number copied out of it.
/// </para>
/// </remarks>
public sealed class SeparatingContactTests
{
    private const int FiveHundredAgents = 500;

    /// <summary>A 500-agent battle is long; the bound is checked over its engagement.</summary>
    private const int FiveHundredAgentTickCap = 1_500;

    /// <summary>The gate workload recorded in <c>docs/development/measurement-history.md</c>.</summary>
    private const int GateAgents = 1_000;

    private const int AllocationWarmUpTicks = 300;

    private const int AllocationMeasuredTicks = 100;

    private const string GateStateHashV14 = "23C1E15282187F73";

    private const string GateEventHashV14 = "4D27E2506BF94FFD";

    private static readonly MovementRuleset V17Rules =
        MovementPresetRegistry.Get(MovementPresetId.SeparatingContactV17);

    [Theory]
    [InlineData(1UL, 200)]
    [InlineData(2UL, 200)]
    [InlineData(3UL, 200)]
    [InlineData(1UL, FiveHundredAgents)]
    public void EveryLivingPairStaysWithinTheBoundedOverlapAfterEveryTick(
        ulong seed,
        int totalAgents)
    {
        var scenario = BuildScenario(seed, totalAgents, MovementPresetId.SeparatingContactV17);
        var simulation = BattleSimulation.Create(scenario);
        var tickCap = totalAgents == FiveHundredAgents
            ? FiveHundredAgentTickCap
            : scenario.TickLimit;
        var minimumDistanceRaw =
            V17Rules.SeparationFloorRaw(scenario.BodyRadiusRaw) -
            (2L * V17Rules.SeparationPushCapRaw(scenario.BodyRadiusRaw));

        AssertNoPairCloserThan(simulation, minimumDistanceRaw, seed);
        while (simulation.Outcome == BattleOutcome.Ongoing && simulation.Tick < tickCap)
        {
            simulation.AdvanceOneTick();
            AssertNoPairCloserThan(simulation, minimumDistanceRaw, seed);
        }
    }

    [Fact]
    public void BodiesDoOverlapUnderV17AndNeverUnderV14()
    {
        var overlapV17 = DeepestOverlapRaw(MovementPresetId.SeparatingContactV17);
        var overlapV14 = DeepestOverlapRaw(MovementPresetId.EvasiveFootworkV14);

        Assert.True(overlapV17 > 0, "V17 never let two bodies overlap; the floor is not in effect.");
        Assert.Equal(0L, overlapV14);
    }

    [Fact]
    public void TwoSameSeedV17RunsProduceIdenticalStateAndEventHashes()
    {
        var first = RunHeadless(300, 3_000, 7UL, MovementPresetId.SeparatingContactV17);
        var second = RunHeadless(300, 3_000, 7UL, MovementPresetId.SeparatingContactV17);

        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(first.EventHash, second.EventHash);
        Assert.True(first.Deterministic);
        Assert.True(second.Deterministic);
    }

    [Fact]
    public void V17DiffersFromV14BecauseTheSeparationPassRuns()
    {
        var v17 = RunHeadless(300, 3_000, 7UL, MovementPresetId.SeparatingContactV17);
        var v14 = RunHeadless(300, 3_000, 7UL, MovementPresetId.EvasiveFootworkV14);

        Assert.NotEqual(v14.StateHash, v17.StateHash);
    }

    /// <summary>
    /// The V14 gate hashes recorded in <c>docs/development/measurement-history.md</c>
    /// must not move: wiring V17 into the tick is gated and may not touch any
    /// other preset.
    /// </summary>
    [Fact]
    public void V14GateWorkloadHashesAreUnchanged()
    {
        var report = RunHeadless(GateAgents, 10_000, 1UL, MovementPresetId.EvasiveFootworkV14);

        Assert.Equal(GateStateHashV14, report.StateHash);
        Assert.Equal(GateEventHashV14, report.EventHash);
        Assert.Equal(0L, report.MaximumPenetrationRaw);
    }

    /// <summary>
    /// The pass and the resolver's floor build their storage once, so a warm
    /// tick under V17 allocates nothing, the same bar the ring search sets for
    /// the shipped presets.
    /// </summary>
    [Fact]
    public void WarmTicksAtOneThousandAgentsAllocateNothingUnderV17()
    {
        var scenario = BuildScenario(1UL, GateAgents, MovementPresetId.SeparatingContactV17);

        // The first melee Attack/Damage in a process allocates once (about 792
        // bytes in GatherAndCommitAttacks), so a throwaway run pays it first.
        var primer = BattleSimulation.Create(scenario);
        for (var tick = 0; tick < AllocationWarmUpTicks + AllocationMeasuredTicks; tick++)
        {
            primer.AdvanceOneTick();
        }

        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < AllocationWarmUpTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < AllocationMeasuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome);
        Assert.True(
            allocatedBytes == 0,
            $"{AllocationMeasuredTicks} warm ticks at {GateAgents} agents allocated " +
            $"{allocatedBytes:N0} bytes; expected none.");
    }

    private static Scenario BuildScenario(
        ulong seed,
        int totalAgents,
        MovementPresetId movementPreset)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
            MovementPreset = movementPreset,
        };
        scenario.Validate();
        return scenario;
    }

    private static long DeepestOverlapRaw(MovementPresetId movementPreset)
    {
        var scenario = BuildScenario(1UL, 200, movementPreset);
        var simulation = BattleSimulation.Create(scenario);
        var contactRaw = 2L * scenario.BodyRadiusRaw;
        var deepestRaw = 0L;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
            deepestRaw = Math.Max(deepestRaw, contactRaw - ClosestLivingPairRaw(simulation));
        }

        return deepestRaw;
    }

    private static long ClosestLivingPairRaw(BattleSimulation simulation)
    {
        var agents = simulation.Agents;
        var closestSquared = long.MaxValue;

        for (var left = 0; left < agents.Count; left++)
        {
            if (!agents[left].IsAlive)
            {
                continue;
            }

            for (var right = left + 1; right < agents.Count; right++)
            {
                if (!agents[right].IsAlive)
                {
                    continue;
                }

                var deltaX = (long)agents[right].XRaw - agents[left].XRaw;
                var deltaY = (long)agents[right].YRaw - agents[left].YRaw;
                closestSquared = Math.Min(
                    closestSquared,
                    (deltaX * deltaX) + (deltaY * deltaY));
            }
        }

        return closestSquared == long.MaxValue
            ? long.MaxValue
            : (long)Math.Floor(Math.Sqrt(closestSquared));
    }

    private static void AssertNoPairCloserThan(
        BattleSimulation simulation,
        long minimumDistanceRaw,
        ulong seed)
    {
        var closestRaw = ClosestLivingPairRaw(simulation);
        if (closestRaw < minimumDistanceRaw)
        {
            Assert.Fail(
                $"Seed {seed} tick {simulation.Tick}: the closest living pair is " +
                $"{closestRaw} raw units apart, below the bounded-overlap floor of " +
                $"{minimumDistanceRaw}.");
        }
    }

    private sealed record HeadlessResult(
        string StateHash,
        string EventHash,
        bool Deterministic,
        long MaximumPenetrationRaw);

    private static HeadlessResult RunHeadless(
        int agents,
        int ticks,
        ulong seed,
        MovementPresetId movementPreset)
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();
        var exitCode = HeadlessRunner.Run(
            [
                "--agents", agents.ToString(CultureInfo.InvariantCulture),
                "--ticks", ticks.ToString(CultureInfo.InvariantCulture),
                "--seed", seed.ToString(CultureInfo.InvariantCulture),
                "--log-level", "off",
                "--preset", ((int)CombatPresetId.PrecolonialPhilippinesV8)
                    .ToString(CultureInfo.InvariantCulture),
                "--movement-preset", ((int)movementPreset)
                    .ToString(CultureInfo.InvariantCulture),
            ],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        using var report = JsonDocument.Parse(output.ToString());
        var root = report.RootElement;
        return new HeadlessResult(
            root.GetProperty("stateHash").GetString()!,
            root.GetProperty("eventHash").GetString()!,
            root.GetProperty("deterministic").GetBoolean(),
            root.GetProperty("collisionMetrics")
                .GetProperty("maximumPenetrationRaw")
                .GetInt64());
    }
}
