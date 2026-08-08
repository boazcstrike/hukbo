using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sandata.Core.Combat;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 52b of docs/plans/2026-08-07-sandata-scaffold.md: the golden replay.
/// Design section 16, verbatim: "The golden replay needs two baselines, not
/// one: a mission with an empty order stream, which is the pure autonomous
/// case, and a mission with a recorded non-empty one. A single empty-stream
/// baseline would prove nothing about the subsystem this section adds."
/// </summary>
/// <remarks>
/// <para>
/// <b>No hash literal in this file.</b> Every expected state hash and event
/// hash lives in <c>Fixtures/seed-1-baseline.json</c>, read at run time by
/// <see cref="LoadFixture"/>. Task 85 pins the one permitted absolute
/// state-hash literal under this test project to
/// <c>MissionStateTests.PreTask79cBaselineHash</c>; this file adds none.
/// </para>
/// <para>
/// <b>Why 8 operators / 40 ticks, not the 200-operator / 10,000-tick
/// benchmark workload.</b> That workload is <c>./scripts/benchmark.ps1</c>'s
/// job, runs for a large fraction of a minute, and is deliberately excluded
/// from the unit-test suite (CLAUDE.md section 4, which
/// records its measured duration). Both missions below reuse
/// <see cref="HeadlessRunner.BuildOpenGrid"/> and
/// <see cref="HeadlessRunner.BuildInitialState"/> — the same dense,
/// alternating-faction packing the benchmark and
/// <c>HeadlessRunnerTests</c> both use — so eight operators at seed 1 are
/// already in contact range of an opposing-faction neighbour from tick 0,
/// which is what keeps this replay from being degenerate at a fraction of
/// the cost.
/// </para>
/// </remarks>
public sealed class GoldenReplayTests
{
    private const int OperatorCount = 8;
    private const int TickCount = 40;
    private const ulong Seed = 1UL;

    // ---- Fixture record shapes -----------------------------------------

    private sealed class BaselineFixture
    {
        public int OperatorCount { get; set; }
        public int TickCount { get; set; }
        public ulong Seed { get; set; }
        public string[] TickStateHashesHex { get; set; } = [];
        public string FinalEventHashHex { get; set; } = string.Empty;
    }

    private sealed class SeedOneBaselineFile
    {
        public BaselineFixture EmptyOrderStream { get; set; } = new();
        public BaselineFixture NonEmptyOrderStream { get; set; } = new();
    }

    private static SeedOneBaselineFile LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-1-baseline.json");

        Assert.True(
            File.Exists(path),
            $"The golden-replay fixture is missing at '{path}'. It is committed under " +
            "tests/Sandata.Core.Tests/Fixtures and copied to the output directory by " +
            "the project's Fixtures item.");

        var json = File.ReadAllText(path);
        var fixture = JsonSerializer.Deserialize<SeedOneBaselineFile>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(fixture);
        return fixture!;
    }

    // ---- Shared fixture builders ----------------------------------------
    // Mirrors TickPipelineTests.BuildMission / BuildGrid: this file may not
    // edit that file, so it grows its own copy rather than a shared
    // dependency across two test files that must stay independently
    // readable.

    private static Mission BuildMission() => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: Seed,
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 1_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: OperatorCount / 2),
            new MissionFactionSetup(FactionId: 1, OperatorCount: OperatorCount / 2)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    /// <summary>
    /// Both baselines replay the exact same seed-1 mission fixture — same
    /// grid, same wall buckets, same initial operator placement — built
    /// through the same internal helpers <c>HeadlessRunnerTests</c> and the
    /// seed-1 benchmark workload already use
    /// (<see cref="HeadlessRunner.BuildOpenGrid"/>,
    /// <see cref="HeadlessRunner.BuildInitialState"/>), so the only variable
    /// between the two baselines below is the order stream each one submits.
    /// </summary>
    private static (Mission Mission, NavGrid Grid, WallBuckets WallBuckets, MissionState InitialState) BuildFixture()
    {
        var (grid, wallBuckets, packingSide) = HeadlessRunner.BuildOpenGrid(OperatorCount);
        var mission = BuildMission();
        var initialState = HeadlessRunner.BuildInitialState(OperatorCount, Seed, packingSide);
        return (mission, grid, wallBuckets, initialState);
    }

    /// <summary>
    /// Runs <paramref name="sim"/> for <see cref="TickCount"/> ticks,
    /// comparing <see cref="SandataSimulation.LastStateHash"/> against
    /// <paramref name="expected"/>'s recorded per-tick hash after every
    /// call to <see cref="SandataSimulation.RunTick"/> and failing, by name,
    /// on the first tick that disagrees — never merely "hashes differ".
    /// </summary>
    private static void RunAndCompareTickByTick(SandataSimulation sim, BaselineFixture expected)
    {
        Assert.Equal(TickCount, expected.TickCount);
        Assert.Equal(TickCount, expected.TickStateHashesHex.Length);

        for (var tick = 0; tick < TickCount; tick++)
        {
            sim.RunTick(tick);

            var actualHex = ToHex(sim.LastStateHash);
            var expectedHex = expected.TickStateHashesHex[tick];

            Assert.True(
                string.Equals(actualHex, expectedHex, StringComparison.Ordinal),
                $"Golden replay diverged: state hash at tick {tick} was {actualHex}, " +
                $"expected {expectedHex} (first mismatch tick = {tick}). " +
                $"{TickCount - tick - 1} further ticks were not compared.");
        }

        var actualEventHashHex = ToHex(sim.State.EventFeed.Hash);
        Assert.True(
            string.Equals(actualEventHashHex, expected.FinalEventHashHex, StringComparison.Ordinal),
            $"Golden replay's every per-tick state hash matched, but the final event hash " +
            $"did not: was {actualEventHashHex}, expected {expected.FinalEventHashHex}. " +
            "The event feed diverged without the state hash noticing.");
    }

    private static string ToHex(ulong value) => value.ToString("X16", CultureInfo.InvariantCulture);

    private static string ToHex(ulong? value) =>
        value is { } present ? ToHex(present) : "(none)";

    // ---- Baseline 1: pure autonomous, empty order stream -----------------

    [Fact]
    public void EmptyOrderStreamBaselineMatchesTheRecordedSeedOneReplay()
    {
        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState, ImmutableArray<CoverRecord>.Empty);

        var fixture = LoadFixture();
        RunAndCompareTickByTick(sim, fixture.EmptyOrderStream);

        // "A baseline recorded from a mission where nothing happens proves
        // nothing" — assert the pure-autonomous run actually fought, not
        // merely that its hash matches a pinned value.
        Assert.NotEmpty(sim.State.EventFeed.Events);
        Assert.Contains(
            sim.State.EventFeed.Events,
            e => e.Kind == MissionEventKind.ShotFired);
        Assert.Contains(
            sim.State.Operators,
            op => op.Health < 100);
    }

    // ---- Baseline 2: recorded, non-empty order stream ---------------------

    [Fact]
    public void NonEmptyOrderStreamBaselineMatchesTheRecordedSeedOneReplay()
    {
        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState, ImmutableArray<CoverRecord>.Empty);

        // The recorded order stream, submitted at tick 0 through
        // SandataSimulation.SubmitOrder — the same public door the game
        // itself uses (ClientOrderDoorTests, OrderQueueTests) — never by
        // constructing an Order or an OrderQueue directly. Both orders
        // target operators that exist in an 8-operator, seed-1 fixture
        // (entity ids 1..8) and a path that stays well inside the fully
        // open 10x10-cell / 40wu grid HeadlessRunner.BuildOpenGrid(8)
        // returns, so neither is rejected for bounds, a blocked cell, or a
        // wall crossing.
        var moveResult = sim.SubmitOrder(
            targetTick: 0,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: ImmutableArray.Create(
                new OrderPathNode(4, 4),
                new OrderPathNode(12, 4)));
        Assert.Null(moveResult.Rejection);
        Assert.NotNull(moveResult.Submitted);

        var holdResult = sim.SubmitOrder(
            targetTick: 0,
            factionId: 1,
            addressees: ImmutableArray.Create(2UL),
            kind: OrderKind.Hold);
        Assert.Null(holdResult.Rejection);
        Assert.NotNull(holdResult.Submitted);

        var fixture = LoadFixture();
        RunAndCompareTickByTick(sim, fixture.NonEmptyOrderStream);

        // Same non-degeneracy bar as the empty-order baseline, plus proof
        // the order layer itself did something observable: the moved
        // operator's assignment is present and stage 1 actually applied it.
        Assert.NotEmpty(sim.State.EventFeed.Events);
        Assert.Contains(
            sim.State.EventFeed.Events,
            e => e.Kind == MissionEventKind.ShotFired);
        Assert.Contains(
            sim.State.Operators,
            op => op.Health < 100);
        Assert.Contains(
            sim.State.OrderAssignments,
            a => a.EntityId == 1UL);

        // Prove the order stream itself moved the needle, not just that the
        // mission fought: re-run the identical fixture with an empty order
        // stream and confirm its final state hash differs from this
        // baseline's. If it did not, the order this test submitted would
        // have had no observable effect on the state hash at all, and this
        // baseline would prove nothing beyond what the empty-order baseline
        // already does.
        var (companionMission, companionGrid, companionWallBuckets, companionInitialState) = BuildFixture();
        var companionSim = new SandataSimulation(
            companionMission,
            SandataRuleset.ModernTacticalV1,
            companionGrid,
            companionWallBuckets,
            companionInitialState,
            ImmutableArray<CoverRecord>.Empty);
        for (var tick = 0; tick < TickCount; tick++)
        {
            companionSim.RunTick(tick);
        }

        Assert.NotEqual(companionSim.LastStateHash, sim.LastStateHash);
    }
}
