using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Determinism;
using Sandata.Core.Events;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 76 of docs/plans/2026-08-07-sandata-scaffold.md, as corrected by the
/// plan's own 2026-08-08 wave-11 audit: <see cref="MissionEventFeed"/>,
/// <see cref="MissionEvent"/>, and <see cref="SandataSimulation.SubmitOrder"/>'s
/// rejected-order emission, exercised only through the production call
/// chain (<see cref="SandataSimulation.SubmitOrder"/>), never by hand-built
/// <see cref="MissionEvent"/> fixtures standing in for it.
/// </summary>
public sealed class MissionEventFeedTests
{
    // ---- Shared fixture builders, matching TickPipelineTests' conventions. ----

    private static Mission BuildMission(ulong seed = 1UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: 1),
            new MissionFactionSetup(FactionId: 1, OperatorCount: 1)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static MissionState BuildEmptyState() => new(
        Tick: 0, Phase: 1, Winner: -1, NextEntityId: 1, NextEventSequence: 0)
    {
        Operators = ImmutableArray<OperatorState>.Empty,
        FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
        Doors = ImmutableArray<DoorState>.Empty,
        Groups = ImmutableArray<GroupPathState>.Empty,
        RngStreams = ImmutableArray<RngStreamState>.Empty,
    };

    private static NavGrid BuildGrid()
    {
        var grid = new NavGrid(width: 8, height: 8);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    /// <summary>
    /// A <see cref="MoveAlongPath"/> submission carrying no nodes always fails
    /// <see cref="OrderValidation.ValidateMoveAlongPath"/>'s node-count rule
    /// (design section 16: "the node count ... is below two"), independent of
    /// grid geometry, walls, or addressees — the cheapest reliable way to
    /// drive a rejection repeatably through the real submission door.
    /// </summary>
    private static (OrderQueue Queue, Order? Submitted, OrderRejection? Rejection) SubmitAlwaysRejectedOrder(
        SandataSimulation simulation, long targetTick) =>
        simulation.SubmitOrder(
            targetTick: targetTick,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: ImmutableArray<OrderPathNode>.Empty);

    // ---- 1. A rejected order emits exactly one readable event. -----------

    [Fact]
    public void SubmitOrder_RejectedOrder_EmitsExactlyOneOrderRejectedEventCarryingIdAndReason()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var sim = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildEmptyState());

        var (_, submitted, rejection) = SubmitAlwaysRejectedOrder(sim, targetTick: 5);

        Assert.Null(submitted);
        Assert.NotNull(rejection);
        Assert.Equal(OrderRejectReason.InvalidNodeCount, rejection!.Reason);

        var emitted = Assert.Single(sim.State.EventFeed.Events);
        Assert.Equal(MissionEventKind.OrderRejected, emitted.Kind);
        Assert.Equal(rejection.OrderId, emitted.SubjectId);
        Assert.Equal((int)OrderRejectReason.InvalidNodeCount, emitted.ReasonCode);
        Assert.Equal(0L, emitted.Sequence);
        Assert.Equal(1L, sim.State.NextEventSequence);
    }

    // ---- 2. The retained feed holds at most 200, drops oldest first. -----

    [Fact]
    public void SubmitOrder_250Rejections_RetainsAtMost200_OldestDropped_NewestLast()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var sim = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildEmptyState());

        const int totalSubmissions = 250;
        for (var i = 0; i < totalSubmissions; i++)
        {
            var (_, _, rejection) = SubmitAlwaysRejectedOrder(sim, targetTick: i);
            Assert.NotNull(rejection);
        }

        var feed = sim.State.EventFeed;
        Assert.Equal(MissionEventFeed.MaxRetainedEvents, feed.Events.Length);
        Assert.Equal(totalSubmissions, sim.State.NextEventSequence);

        // Oldest retained event is submission #51 (sequence 50, zero-based);
        // newest retained is submission #250 (sequence 249).
        Assert.Equal(totalSubmissions - MissionEventFeed.MaxRetainedEvents, feed.Events[0].Sequence);
        Assert.Equal(totalSubmissions - 1, feed.Events[^1].Sequence);
    }

    // ---- 3. The event hash accumulates beyond the 200-event cap. ---------

    [Fact]
    public void EventHash_Of250Emissions_DiffersFromHashRecomputedOverTheRetained200()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var sim = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildEmptyState());

        const int totalSubmissions = 250;
        for (var i = 0; i < totalSubmissions; i++)
        {
            SubmitAlwaysRejectedOrder(sim, targetTick: i);
        }

        var feed = sim.State.EventFeed;

        // Rebuild a feed from only the 200 events the production feed still
        // retains -- if the cap had truncated the hash, this recomputed
        // value would equal feed.Hash. It must not: feed.Hash folded all 250
        // emissions, not merely the 200 the cap left behind.
        var recomputedFromRetainedWindowOnly = MissionEventFeed.Empty;
        foreach (var retained in feed.Events)
        {
            recomputedFromRetainedWindowOnly = recomputedFromRetainedWindowOnly.Append(retained);
        }

        Assert.NotEqual(recomputedFromRetainedWindowOnly.Hash, feed.Hash);
    }

    // ---- 4. The state hash is pinned before this task's edits and does not ----
    // ---- move when the event feed gains events. ---------------------------

    // -- Fixtures copied field-for-field from OrderStateHashTests.BuildSampleState() / -
    // -- BuildSampleMission(), so the pinned baseline below matches the pre-task-76 ----
    // -- hasher's own recorded value for the identical input. ---------------------------

    private static Mission BuildPinnedMission(ulong seed = 12_345UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 999UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 50),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 1, OperatorCount: 4),
            new MissionFactionSetup(FactionId: 0, OperatorCount: 4)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildPinnedOperator(int entityId) => new(
        EntityId: (ulong)entityId,
        PositionX: FixedPoint.FromWhole(entityId),
        PositionY: FixedPoint.FromWhole(entityId * 2),
        Facing: Facing16.East,
        AimAngle: Bam16.FromFacing16(Facing16.East),
        Health: 100,
        Faction: entityId % 2,
        Intent: 1,
        IsCrouched: false,
        WeaponLowered: false,
        WeaponChainPhase: 0,
        WeaponChainRemainingTicks: 5,
        MagazineRounds: 30,
        CyclicFireAccumulator: 0,
        SuppressionCounter: 0)
    {
        ContactMemory = ImmutableArray.Create(new ContactMemoryEntry(99UL, 5, 1, 10)),
    };

    private static MissionState BuildPinnedState() => new(
        Tick: 42, Phase: 1, Winner: -1, NextEntityId: 8, NextEventSequence: 3)
    {
        Operators = ImmutableArray.Create(BuildPinnedOperator(1), BuildPinnedOperator(2)),
        FactionAlerts = ImmutableArray.Create(
            new FactionAlertState(0, 0),
            new FactionAlertState(1, 1)),
        Doors = ImmutableArray.Create(
            new DoorState(1, true, 10),
            new DoorState(2, false, 20)),
        Groups = ImmutableArray.Create(
            new GroupPathState(1, 100, true, 50, 200, 30)),
        RngStreams = ImmutableArray.Create(
            new RngStreamState(1, 1, 111UL, 222UL)),
    };

    /// <summary>
    /// Identical to <c>OrderStateHashTests.PreTask61BaselineHash</c>, for the
    /// identical fixture. <see cref="SandataStateHasher.Compute"/> was not
    /// edited by task 76 (confirmed by reading the file in full before and
    /// after this task's changes) and never reads
    /// <see cref="MissionState.EventFeed"/>, so this value is unchanged from
    /// task 61's own pin — this constant exists under its own name only so a
    /// future reader searching for "what did task 76 pin" finds an answer
    /// here rather than having to already know it is task 61's value.
    /// </summary>
    // Moved at task 79c, from 5_550_901_129_500_655_850UL, for the reason
    // recorded beside OrderStateHashTests.PreTask61BaselineHash: task 79c
    // appends the operator's Firearm to FoldOperator. What this test guards
    // is unaffected — Compute still never reads MissionState.EventFeed, and
    // StateHash_DoesNotMove_WhenTheEventFeedGainsEvents below is the
    // assertion that actually proves it, independently of any literal.
    private const ulong PreTask76BaselineHash = 3_159_438_799_659_597_482UL;

    [Fact]
    public void StateHash_OfPinnedFixtureWithDefaultEventFeed_MatchesThePreTask76Baseline()
    {
        var mission = BuildPinnedMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var state = BuildPinnedState();

        Assert.Equal(MissionEventFeed.Empty, state.EventFeed);

        var hash = SandataStateHasher.Compute(mission, state, ruleset);

        Assert.Equal(PreTask76BaselineHash, hash);
    }

    [Fact]
    public void StateHash_DoesNotMove_WhenTheEventFeedGainsEvents()
    {
        var mission = BuildPinnedMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var baselineState = BuildPinnedState();
        var stateWithEvents = baselineState with
        {
            EventFeed = baselineState.EventFeed.Append(
                MissionEvent.OrderRejected(0, baselineState.Tick, orderId: 7, OrderRejectReason.InvalidNodeCount)),
        };

        var baselineHash = SandataStateHasher.Compute(mission, baselineState, ruleset);
        var hashWithEvents = SandataStateHasher.Compute(mission, stateWithEvents, ruleset);

        Assert.Equal(baselineHash, hashWithEvents);
        Assert.NotEqual(baselineState.EventFeed, stateWithEvents.EventFeed);
    }

    // ---- 5. Snapshot round trip reproduces the same event hash and -------
    // ---- the same NextEventSequence. --------------------------------------

    [Fact]
    public void Snapshot_RoundTrip_ReproducesTheSameEventHashAndNextEventSequence()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var sim = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildEmptyState());

        for (var i = 0; i < 5; i++)
        {
            SubmitAlwaysRejectedOrder(sim, targetTick: i);
        }

        var original = sim.State;
        var restored = original.ToSnapshot().ToState();

        Assert.Equal(original.NextEventSequence, restored.NextEventSequence);
        Assert.Equal(original.EventFeed.Hash, restored.EventFeed.Hash);
        Assert.Equal(original.EventFeed.Events, restored.EventFeed.Events);
        Assert.Equal(original, restored);
    }

    // ---- 6. Same mission, same submissions, twice: identical ordered -----
    // ---- event stream and identical event hash. ---------------------------

    [Fact]
    public void TwoRunsWithIdenticalSubmissionsAndTicks_ProduceIdenticalEventStreamsAndEventHash()
    {
        var gridA = BuildGrid();
        var wallBucketsA = NoWalls(gridA);
        var gridB = BuildGrid();
        var wallBucketsB = NoWalls(gridB);

        var simA = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, gridA, wallBucketsA, BuildEmptyState());
        var simB = new SandataSimulation(BuildMission(), SandataRuleset.ModernTacticalV1, gridB, wallBucketsB, BuildEmptyState());

        for (var tick = 0; tick < 12; tick++)
        {
            SubmitAlwaysRejectedOrder(simA, targetTick: tick);
            simA.RunTick(tick);

            SubmitAlwaysRejectedOrder(simB, targetTick: tick);
            simB.RunTick(tick);
        }

        // Compared via SequenceEqual, not Assert.Equal(ImmutableArray<T>, ...)
        // directly: xunit's default structural comparer for ImmutableArray<T>
        // does not reliably fall back to element-wise equality for record
        // structs, which produced spurious failures against otherwise
        // identical sequences during authoring. SequenceEqual is what
        // MissionEventFeed.Equals itself uses, so this assertion matches
        // production equality semantics exactly.
        Assert.True(
            simA.State.EventFeed.Events.AsSpan().SequenceEqual(simB.State.EventFeed.Events.AsSpan()),
            "event streams diverged between two runs of identical submissions");
        Assert.Equal(simA.State.EventFeed.Hash, simB.State.EventFeed.Hash);
        Assert.Equal(simA.State.NextEventSequence, simB.State.NextEventSequence);
    }
}
