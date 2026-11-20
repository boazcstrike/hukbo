using System.Globalization;
using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
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

    private const int ThousandAgents = 1_000;

    /// <summary>The gate workload recorded in <c>docs/development/measurement-history.md</c>.</summary>
    private const int GateAgents = ThousandAgents;

    private const int AllocationWarmUpTicks = 300;

    private const int AllocationMeasuredTicks = 100;

    /// <summary>
    /// The production body radius <c>R</c> in raw units. Pinned so the literal
    /// bound below cannot rot silently if the default radius moves.
    /// </summary>
    private const int ProductionBodyRadiusRaw = 4_352;

    /// <summary>
    /// The bounded-overlap floor <c>2R(1 - f) - 2c</c> for the production radius
    /// under V17: <c>7,398 - 2 * 217</c>, with <c>f = 0.15</c> and
    /// <c>c = 0.05R</c>. A literal, so a tuning change that moves the bound is a
    /// deliberate edit here and not a test that follows the ruleset.
    /// </summary>
    private const long BoundedOverlapFloorRaw = 6_964;

    /// <summary>The 200-agent combat-8 movement-14 workload of <c>docs/development/testing.md</c>, "The seven Hukbo workloads".</summary>
    private const int WorkloadAgents = 200;

    private const int WorkloadTicks = 10_000;

    private const string WorkloadStateHashV14 = "7A1838EB4D835A45";

    private const string WorkloadEventHashV14 = "447727A6E82FB9FD";

    private static readonly MovementRuleset V17Rules =
        MovementPresetRegistry.Get(MovementPresetId.SeparatingContactV17);

    [Theory]
    [InlineData(1UL, 200)]
    [InlineData(2UL, 200)]
    [InlineData(3UL, 200)]
    [InlineData(1UL, FiveHundredAgents)]
    [InlineData(1UL, ThousandAgents)]
    public void EveryLivingPairStaysWithinTheBoundedOverlapAfterEveryTick(
        ulong seed,
        int totalAgents)
    {
        var scenario = BuildScenario(seed, totalAgents, MovementPresetId.SeparatingContactV17);
        Assert.Equal(ProductionBodyRadiusRaw, scenario.BodyRadiusRaw);
        Assert.Equal(
            BoundedOverlapFloorRaw,
            V17Rules.SeparationFloorRaw(scenario.BodyRadiusRaw) -
            (2L * V17Rules.SeparationPushCapRaw(scenario.BodyRadiusRaw)));

        var simulation = BattleSimulation.Create(scenario);

        AssertNoPairCloserThan(simulation, BoundedOverlapFloorRaw, seed);
        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
            AssertNoPairCloserThan(simulation, BoundedOverlapFloorRaw, seed);
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
    /// The V14 workload hashes recorded in <c>docs/development/testing.md</c>
    /// ("The seven Hukbo workloads", combat 8 movement 14) must not move: wiring
    /// V17 into the tick is gated and may not touch any other preset.
    /// </summary>
    [Fact]
    public void V14WorkloadHashesAreUnchanged()
    {
        var report = RunHeadless(
            WorkloadAgents,
            WorkloadTicks,
            1UL,
            MovementPresetId.EvasiveFootworkV14);

        Assert.Equal(WorkloadStateHashV14, report.StateHash);
        Assert.Equal(WorkloadEventHashV14, report.EventHash);
        Assert.Equal(0L, report.MaximumPenetrationRaw);
    }

    /// <summary>
    /// V17 is V14 plus the separation pass and nothing else. V17 can only differ
    /// from V14 on a tick where V14's resolver refused or bent a proposal (a
    /// lower floor accepts what V14 refused, and the pass acts only on a pair
    /// inside <c>2R</c>). So every tick before V14's first such intervention
    /// must leave every agent view identical, which fails if any of V17's
    /// preset-identity gates drifts from V14's. State hashes are not compared:
    /// the preset id is folded into them.
    /// </summary>
    /// <remarks>
    /// A crowded battle collides within a few ticks, so its shared prefix is
    /// short; the small battles are what carry the footwork gates through a long
    /// approach. The minimum is the measured prefix pinned a little below.
    /// </remarks>
    [Theory]
    [InlineData(1UL, 200, 1)]
    [InlineData(1UL, 2, 95)]
    [InlineData(4UL, 2, 100)]
    [InlineData(2UL, 4, 96)]
    [InlineData(1UL, 20, 19)]
    public void V17IsLockstepIdenticalToV14UntilTheFirstCollisionIntervention(
        ulong seed,
        int totalAgents,
        int minimumComparedTicks)
    {
        var v14 = BattleSimulation.Create(
            BuildScenario(seed, totalAgents, MovementPresetId.EvasiveFootworkV14));
        var v17 = BattleSimulation.Create(
            BuildScenario(seed, totalAgents, MovementPresetId.SeparatingContactV17));
        var comparedTicks = 0;
        var interventionTick = -1L;

        while (v14.Outcome == BattleOutcome.Ongoing)
        {
            v14.AdvanceOneTick();
            v17.AdvanceOneTick();

            if (v14.Agents.Any(agent => agent.MovementResolution
                is MovementResolution.Truncated
                or MovementResolution.Slid
                or MovementResolution.Blocked
                or MovementResolution.Separated))
            {
                interventionTick = v14.Tick;
                break;
            }

            AssertViewsIdentical(v14, v17);
            comparedTicks++;
        }

        Assert.True(interventionTick > 0, "V14's resolver never intervened to end the lockstep.");
        Assert.True(
            comparedTicks >= minimumComparedTicks,
            $"Seed {seed}, {totalAgents} agents: only {comparedTicks} ticks were compared " +
            $"before V14's first collision intervention at tick {interventionTick}.");
    }

    /// <summary>
    /// Proves the pass is wired into the tick and its result written back: the
    /// resolver's floor alone leaves a pair that starts inside the soft band
    /// exactly where it is.
    /// </summary>
    [Fact]
    public void ThePassPushesAStandingPairInsideTheSoftBandApartUnderV17()
    {
        var scenario = BuildIdlePairScenario(MovementPresetId.SeparatingContactV17);
        var floorRaw = V17Rules.SeparationFloorRaw(scenario.BodyRadiusRaw);
        var startRaw = (floorRaw + (2L * scenario.BodyRadiusRaw)) / 2;
        Assert.InRange(startRaw, floorRaw + 1L, (2L * scenario.BodyRadiusRaw) - 1L);

        var simulation = BuildIdlePair(scenario, (int)startRaw);
        simulation.AdvanceOneTick();

        Assert.True(
            DistanceRaw(simulation.Agents[0], simulation.Agents[1]) > startRaw,
            "The separation pass did not push a pair inside the soft band apart.");
    }

    [Fact]
    public void AStandingPairAtContactDistanceDoesNotMoveUnderV14()
    {
        var scenario = BuildIdlePairScenario(MovementPresetId.EvasiveFootworkV14);
        var contactRaw = (int)(2L * scenario.BodyRadiusRaw);

        var simulation = BuildIdlePair(scenario, contactRaw);
        simulation.AdvanceOneTick();

        Assert.Equal(contactRaw, DistanceRaw(simulation.Agents[0], simulation.Agents[1]));
    }

    /// <summary>
    /// The floor and push cap are written twice, once on the ruleset the
    /// resolver is built from and once on the pass's parameters. They must agree
    /// for every radius, or the resolver and the pass disagree on the band.
    /// </summary>
    [Theory]
    [InlineData(ProductionBodyRadiusRaw)]
    [InlineData(1_001)]
    [InlineData(512)]
    public void TheRulesetAndThePassParametersAgreeOnTheFloorAndThePushCap(int bodyRadiusRaw)
    {
        var parameters = new SeparationParameters(
            bodyRadiusRaw,
            V17Rules.SeparationSoftBandBasisPoints,
            V17Rules.SeparationPushCapRadiusBasisPoints,
            V17Rules.SeparationStiffnessBasisPoints,
            V17Rules.SeparationCommittedWeightMultiplier,
            V17Rules.SeparationSidewaysBasisPoints,
            ArenaWidthRaw: 1_000_000,
            ArenaHeightRaw: 1_000_000);

        Assert.Equal(V17Rules.SeparationFloorRaw(bodyRadiusRaw), parameters.FloorRaw);
        Assert.Equal(V17Rules.SeparationPushCapRaw(bodyRadiusRaw), parameters.PushCapRaw);
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

    /// <summary>
    /// A scenario whose two factions are far beyond perception range of each
    /// other, so a same-faction pair stands still: nothing targets, nothing
    /// attacks, and any movement is the separation pass.
    /// </summary>
    private static Scenario BuildIdlePairScenario(MovementPresetId movementPreset) =>
        BuildScenario(1UL, 2, movementPreset) with
        {
            PerceptionRangeRaw = 100 * FixedPoint.Scale,
        };

    private static BattleSimulation BuildIdlePair(Scenario scenario, int pairDistanceRaw)
    {
        const int pairXRaw = 200 * FixedPoint.Scale;
        const int pairYRaw = 100 * FixedPoint.Scale;
        const int enemyXRaw = 1_100 * FixedPoint.Scale;
        const int enemyYRaw = 650 * FixedPoint.Scale;
        var loadout = new CombatLoadout(
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.None);

        AgentState Agent(ulong entityId, int factionId, int xRaw, int yRaw) =>
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
                loadout);

        return BattleSimulation.CreateForTesting(
            scenario,
            Agent(1UL, 0, pairXRaw, pairYRaw),
            Agent(2UL, 0, pairXRaw + pairDistanceRaw, pairYRaw),
            Agent(3UL, 1, enemyXRaw, enemyYRaw));
    }

    private static long DistanceRaw(AgentView left, AgentView right)
    {
        var deltaX = (long)right.XRaw - left.XRaw;
        var deltaY = (long)right.YRaw - left.YRaw;
        return (long)Math.Floor(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
    }

    private static void AssertViewsIdentical(BattleSimulation expected, BattleSimulation actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Agents.Count, actual.Agents.Count);
        for (var index = 0; index < expected.Agents.Count; index++)
        {
            var left = expected.Agents[index];
            var right = actual.Agents[index];
            Assert.True(
                left == right,
                $"Tick {expected.Tick}, entity {left.EntityId}: V14 {left} differs from V17 {right}.");
        }
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
