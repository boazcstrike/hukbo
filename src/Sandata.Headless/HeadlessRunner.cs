using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Diagnostics;
using Sandata.Core.Combat;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Headless;

/// <summary>
/// Task 51 of docs/plans/2026-08-07-sandata-scaffold.md: builds a seeded
/// mission fixture, runs it to completion on two independent
/// <see cref="SandataSimulation"/> instances in lockstep, and reports whether
/// they ever disagreed — Sandata's analogue of
/// <c>Hukbo.Headless.HeadlessRunner.Execute</c>. Unlike that precedent, this
/// type owns no argument parsing and touches no console: <c>Program</c>
/// keeps sole ownership of both, per <c>CLAUDE.md</c> section 5's "only the
/// two <c>Program.cs</c> entry points touch the console."
/// </summary>
/// <remarks>
/// <b>No expected-hash literal anywhere in this file.</b> See
/// <see cref="RunReport"/>'s own remarks for why: task 77, landing in the
/// same batch, moves <see cref="SandataRuleset.ContentHash"/>, which moves
/// every mission hash with it. Every check this type performs is a
/// self-consistency check between the two simulations it runs itself, never
/// a comparison against a value typed into this file or a test file.
/// </remarks>
public static class HeadlessRunner
{
    /// <summary>
    /// PROVISIONAL default operator count when <c>--agents</c> is not
    /// supplied — matches <c>Hukbo.Headless</c>'s own default of 200,
    /// unrelated to any measured Sandata capacity limit.
    /// </summary>
    public const int DefaultOperatorCount = 200;

    /// <summary>
    /// PROVISIONAL default tick count when <c>--ticks</c> is not supplied —
    /// matches <c>Hukbo.Headless</c>'s own default of 10,000.
    /// </summary>
    public const int DefaultTickCount = 10_000;

    /// <summary>
    /// PROVISIONAL default seed when <c>--seed</c> is not supplied — matches
    /// <c>Hukbo.Headless</c>'s own default of 1.
    /// </summary>
    public const ulong DefaultSeed = 1;

    /// <summary>
    /// PROVISIONAL upper bound on <c>--agents</c>. Not a measured Sandata
    /// capacity limit — <see cref="Sandata.Core.Simulation.Mission"/> itself
    /// imposes no such cap — chosen only to keep <see cref="NavGrid"/> (whose
    /// own <see cref="NavGrid.MaxDimensionCells"/> is 512 per axis) able to
    /// place one operator per cell with room to spare, and to keep an
    /// unbounded <c>--agents</c> value from turning a benchmark invocation
    /// into an unintentional stress test.
    /// </summary>
    public const int MaxOperatorCount = 20_000;

    /// <summary>
    /// PROVISIONAL: the fixture's mission never ends by its own tick limit
    /// before the caller-requested tick count does, since <c>--ticks</c>
    /// itself already bounds the loop below. A large fixed ceiling, not a
    /// measured value, keeps <see cref="MissionTickPolicy"/>'s own positive
    /// tick-limit requirement satisfied regardless of <c>--ticks</c>.
    /// </summary>
    private const int FixtureTickLimit = 100_000_000;

    /// <summary>
    /// Called once per tick, immediately before the right simulation's own
    /// <see cref="SandataSimulation.RunTick"/>, giving a test the chance to
    /// submit an order only the right simulation ever sees — the seam
    /// <c>Sandata.Core.Tests.HeadlessRunnerTests</c> uses to prove the
    /// mismatch path deterministically, entirely through
    /// <see cref="SandataSimulation.SubmitOrder"/>, without editing anything
    /// under <c>src/Sandata.Core</c>.
    /// </summary>
    internal delegate void CorruptionInjector(SandataSimulation rightSimulation, long tick);

    /// <summary>
    /// Runs the seeded determinism workload <paramref name="operatorCount"/>
    /// operators (split evenly across the two factions) for up to
    /// <paramref name="tickCount"/> ticks, or until the two simulations
    /// disagree, whichever comes first.
    /// </summary>
    /// <param name="operatorCount">
    /// Total operator count across both factions. Must be a positive, even
    /// integer no greater than <see cref="MaxOperatorCount"/> — validated
    /// here again even though <c>Program</c> validates it first, so a direct
    /// test caller gets the same guarantee.
    /// </param>
    /// <param name="tickCount">The number of ticks to run. Must be positive.</param>
    /// <param name="seed">
    /// The mission seed. Folds into <see cref="Mission.MissionContentHash"/>
    /// and seeds this method's own <see cref="SplitMix64"/> fixture-layout
    /// generator, so two calls with the same seed place operators
    /// identically and, since both a run's own two simulations and two
    /// separate runs of this method start from that identical fixture,
    /// produce identical results.
    /// </param>
    public static RunReport Execute(int operatorCount, int tickCount, ulong seed, DiagnosticLog log) =>
        Execute(operatorCount, tickCount, seed, log, corruptRightSimulation: null);

    /// <summary>
    /// The corruption-seam overload. Internal — <c>Sandata.Core.Tests.HeadlessRunnerTests</c>
    /// reaches it through the <c>InternalsVisibleTo("Sandata.Core.Tests")</c>
    /// grant this assembly already carries for task 50's own argument tests.
    /// </summary>
    internal static RunReport Execute(
        int operatorCount,
        int tickCount,
        ulong seed,
        DiagnosticLog log,
        CorruptionInjector? corruptRightSimulation)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (operatorCount <= 0 || (operatorCount & 1) != 0 || operatorCount > MaxOperatorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operatorCount),
                operatorCount,
                $"Operator count must be a positive even integer no greater than {MaxOperatorCount}.");
        }

        if (tickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount, "Tick count must be positive.");
        }

        var (grid, wallBuckets, packingSide) = BuildOpenGrid(operatorCount);
        var mission = BuildMission(seed, operatorCount);
        var initialState = BuildInitialState(operatorCount, seed, packingSide);

        // Both simulations start from the same immutable fixture: MissionState
        // is a record whose every collection is an ImmutableArray, so sharing
        // one instance as each simulation's starting point cannot let one
        // simulation's later mutation reach the other. NavGrid and
        // WallBuckets are likewise shared read-only inputs, exactly as
        // TickPipelineTests.BuildGrid/NoWalls share theirs across simA/simB.
        var left = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState);
        var right = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState);

        var tickDurations = new List<double>(Math.Min(tickCount, 100_000));
        long? firstMismatchTick = null;
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        long measuredTicks = 0;

        for (var tick = 0; tick < tickCount; tick++)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            left.RunTick(tick);
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            tickDurations.Add(elapsed.TotalMilliseconds);
            measuredTicks = tick + 1;

            corruptRightSimulation?.Invoke(right, tick);
            right.RunTick(tick);

            if (left.State.Tick != right.State.Tick ||
                left.State.Winner != right.State.Winner ||
                left.LastStateHash != right.LastStateHash ||
                left.State.EventFeed.Hash != right.State.EventFeed.Hash ||
                !left.State.EventFeed.Events.SequenceEqual(right.State.EventFeed.Events))
            {
                firstMismatchTick = tick;
                log.SetTick(tick);
                log.Write(
                    LogLevel.Error,
                    LogChannel.Simulation,
                    LogEvents.SimMismatch,
                    "leftTick",
                    left.State.Tick,
                    "rightTick",
                    right.State.Tick,
                    "leftStateHash",
                    ToHex(left.LastStateHash),
                    "rightStateHash",
                    ToHex(right.LastStateHash),
                    "leftEventHash",
                    ToHex(left.State.EventFeed.Hash),
                    "rightEventHash",
                    ToHex(right.State.EventFeed.Hash));
                break;
            }
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var sortedDurations = tickDurations.Order().ToArray();
        var totalDuration = tickDurations.Sum();
        var survivors0 = left.State.Operators.Count(op => op.Faction == 0 && DamageResolution.IsAlive(op.Health));
        var survivors1 = left.State.Operators.Count(op => op.Faction == 1 && DamageResolution.IsAlive(op.Health));
        var outcome = (MissionOutcome)left.State.Winner;

        log.SetTick(left.State.Tick);
        log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimOutcome,
            "outcome",
            outcome.ToString(),
            "tick",
            left.State.Tick,
            "survivors0",
            survivors0,
            "survivors1",
            survivors1,
            "stateHash",
            ToHex(left.LastStateHash),
            "eventHash",
            ToHex(left.State.EventFeed.Hash));

        return new RunReport(
            new RunEnvironment(
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessorCount),
            seed,
            operatorCount / 2,
            tickCount,
            measuredTicks,
            totalDuration,
            new TickPercentiles(
                Percentile(sortedDurations, 0.50),
                Percentile(sortedDurations, 0.95),
                Percentile(sortedDurations, 0.99),
                sortedDurations.Length == 0 ? 0 : sortedDurations[^1]),
            allocatedBytes,
            outcome.ToString(),
            survivors0,
            survivors1,
            ToHex(left.State.EventFeed.Hash),
            ToHex(left.LastStateHash),
            firstMismatchTick is null,
            firstMismatchTick);
    }

    /// <summary>
    /// The body diameter, in whole world units, the fixture's placement must
    /// clear: task 86 (docs/plans/2026-08-07-sandata-scaffold.md, "wave-12
    /// audit") set <see cref="SandataSimulation.CollisionBodyRadiusRaw"/> to
    /// 4,352 raw — design section 4's 4.25 wu — so the diameter is 8.5 wu,
    /// rounded up to a whole unit because this fixture's placement arithmetic
    /// is whole-world-unit-only, same as <see cref="NavGrid.CellSizeWu"/>
    /// itself. <see cref="SandataSimulation.CollisionBodyRadiusRaw"/> is
    /// <see langword="private"/> and this assembly carries no
    /// <c>InternalsVisibleTo</c> grant from <c>Sandata.Core</c>, so the value
    /// is restated from the same design citation rather than referenced live
    /// — widening that visibility surface is outside this task's file grant.
    /// </summary>
    private const int OperatorBodyDiameterWu = 9;

    /// <summary>
    /// The worst-case shrink, in whole world units, <see cref="BuildInitialState"/>'s
    /// own per-axis jitter can impose on two placed operators' centre-to-centre
    /// separation: each operator's jitter independently spans
    /// <c>[-1, 1]</c> wu per axis, so two adjacent operators jittered toward
    /// each other close the gap by at most 1 + 1 = 2 wu.
    /// </summary>
    private const int JitterWorstCaseShrinkWu = 2;

    /// <summary>
    /// The minimum centre-to-centre pitch, in whole world units, two placed
    /// operators must start at so that even the jitter's worst case in
    /// <see cref="JitterWorstCaseShrinkWu"/> cannot let their bodies overlap.
    /// </summary>
    private const int MinimumOperatorPitchWu = OperatorBodyDiameterWu + JitterWorstCaseShrinkWu;

    /// <summary>
    /// How many <see cref="NavGrid"/> cells apart, on each axis, adjacent
    /// operators are placed — the smallest whole cell count whose physical
    /// pitch (<c>cells * NavGrid.CellSizeWu</c>) is at least
    /// <see cref="MinimumOperatorPitchWu"/>, derived rather than chosen.
    /// Ceiling integer division: <c>(a + b - 1) / b</c>.
    /// </summary>
    private const int OperatorSpacingPitchCells =
        (MinimumOperatorPitchWu + NavGrid.CellSizeWu - 1) / NavGrid.CellSizeWu;

    /// <summary>
    /// A fresh, fully open <see cref="NavGrid"/> sized to place one operator
    /// per <see cref="OperatorSpacingPitchCells"/>-pitched cell with room to
    /// spare, plus its matching wall-free <see cref="WallBuckets"/> — this
    /// workload measures the tick pipeline's own cost, not pathfinding around
    /// obstacles, so no wall is authored. Mirrors
    /// <c>TickPipelineTests.BuildGrid</c>/<c>NoWalls</c>.
    /// </summary>
    /// <returns>
    /// The grid and its wall buckets, plus <c>PackingSide</c>: the logical
    /// row width <see cref="BuildInitialState"/> must use for its own
    /// <c>index % PackingSide</c> layout, which is narrower than the
    /// returned <see cref="NavGrid"/>'s actual <see cref="NavGrid.Width"/>
    /// now that placement is pitched — <see cref="NavGrid.Width"/> spans the
    /// pitched physical footprint, not the one-operator-per-logical-cell
    /// count.
    /// </returns>
    internal static (NavGrid Grid, WallBuckets WallBuckets, int PackingSide) BuildOpenGrid(int operatorCount)
    {
        // Each operator gets its own logical cell (see BuildInitialState),
        // so the packing side must fit at least operatorCount logical cells.
        // Square, and padded so a small roster is not squeezed onto a
        // one-cell-wide grid.
        var packingSide = Math.Clamp((int)Math.Ceiling(Math.Sqrt(operatorCount)), 4, NavGrid.MaxDimensionCells);

        // The physical NavGrid must be wide enough to hold the highest
        // pitched logical coordinate in bounds — see LineOfSight.IsVisible's
        // requirement (via GridRay.Traverse) that every queried origin cell
        // lie inside the grid, which every sensing operator's own position
        // is.
        var physicalSide = Math.Clamp(
            ((packingSide - 1) * OperatorSpacingPitchCells) + 1, 4, NavGrid.MaxDimensionCells);
        var grid = new NavGrid(physicalSide, physicalSide);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        return (grid, wallBuckets, packingSide);
    }

    /// <summary>
    /// The mission wrapper around the workload's parameters.
    /// <see cref="MissionTickPolicy.StateHashCadenceTicks"/> is 1 so
    /// <see cref="SandataSimulation.LastStateHash"/> is fresh after every
    /// call to <see cref="SandataSimulation.RunTick"/>, matching
    /// <c>TickPipelineTests.BuildMission</c>'s own reason for the same
    /// choice.
    /// </summary>
    private static Mission BuildMission(ulong seed, int operatorCount) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        // No map is loaded by this workload (BuildOpenGrid builds the grid
        // directly), so there is no real map content to hash. 1 is an
        // arbitrary non-zero placeholder, exactly as TickPipelineTests.BuildMission
        // uses for the same reason.
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: FixtureTickLimit, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: operatorCount / 2),
            new MissionFactionSetup(FactionId: 1, OperatorCount: operatorCount / 2)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    /// <summary>
    /// Places <paramref name="operatorCount"/> operators one per logical
    /// cell of a <paramref name="packingSide"/>-wide row-major grid, each
    /// logical cell <see cref="OperatorSpacingPitchCells"/> physical
    /// <see cref="NavGrid"/> cells apart on both axes so no two operators'
    /// designed bodies start overlapping (task 86; see
    /// <see cref="MinimumOperatorPitchWu"/>), alternating faction by index so
    /// every logical cell has a mixed-faction neighbour — deliberately denser
    /// contact than a two-line-of-battle layout, so the workload actually
    /// exercises stage 5's sensing, stage 6's squad grouping, stage 10's
    /// collision, and stage 12/13's fire resolution rather than mostly
    /// idling. Faction 0 faces Facing16.East and faction 1 faces
    /// Facing16.West, per each member's own doc comment naming this as that
    /// faction's initial facing. <see cref="SplitMix64"/>, seeded from
    /// <paramref name="seed"/>, jitters each operator a whole world unit
    /// inside its cell so a run's spatial layout is itself seed-dependent,
    /// not just its downstream tick-by-tick decisions. The jitter draw order
    /// and count are unchanged by the pitch: exactly two <see cref="SplitMix64.NextInt"/>
    /// calls per operator, in the same index order, regardless of where the
    /// pitch places that operator's unjittered cell centre.
    /// </summary>
    internal static MissionState BuildInitialState(int operatorCount, ulong seed, int packingSide)
    {
        var rng = new SplitMix64(seed);
        var operators = ImmutableArray.CreateBuilder<OperatorState>(operatorCount);

        for (var index = 0; index < operatorCount; index++)
        {
            var cellX = (index % packingSide) * OperatorSpacingPitchCells;
            var cellY = (index / packingSide) * OperatorSpacingPitchCells;
            var jitterX = rng.NextInt(3) - 1;
            var jitterY = rng.NextInt(3) - 1;
            var worldX = (cellX * NavGrid.CellSizeWu) + (NavGrid.CellSizeWu / 2) + jitterX;
            var worldY = (cellY * NavGrid.CellSizeWu) + (NavGrid.CellSizeWu / 2) + jitterY;
            var faction = index % 2;
            var facing = faction == 0 ? Facing16.East : Facing16.West;

            operators.Add(new OperatorState(
                EntityId: (ulong)(index + 1),
                PositionX: FixedPoint.FromWhole(worldX),
                PositionY: FixedPoint.FromWhole(worldY),
                Facing: facing,
                AimAngle: Bam16.FromFacing16(facing),
                Health: 100,
                Faction: faction,
                Intent: 0,
                IsCrouched: false,
                WeaponLowered: false,
                WeaponChainPhase: 0,
                WeaponChainRemainingTicks: 0,
                MagazineRounds: 30,
                CyclicFireAccumulator: 0,
                SuppressionCounter: 0));
        }

        var built = operators.MoveToImmutable();
        return new MissionState(
            Tick: 0, Phase: 1, Winner: -1, NextEntityId: (ulong)(operatorCount + 1), NextEventSequence: 0)
        {
            Operators = built,
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,
            Groups = ImmutableArray<GroupPathState>.Empty,
            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };
    }

    private static string ToHex(ulong? value) =>
        value is { } present ? present.ToString("X16", CultureInfo.InvariantCulture) : "(none)";

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }
}
