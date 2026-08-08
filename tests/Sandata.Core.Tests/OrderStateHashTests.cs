using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Determinism;
using Sandata.Core.Mathematics;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 61 of docs/plans/2026-08-07-sandata-scaffold.md: <see cref="MissionState.OrderQueue"/>
/// and <see cref="MissionState.OrderAssignments"/> against design section 16,
/// "Order records and the queue," "An authored polyline is authoritative, not
/// derived," and "What this does to the determinism contract." The four
/// groups below match the plan row's own "Done when" text and this task's
/// prompt: a per-field hash-move proof, a structural proof that the field
/// order every field before this task already used is undisturbed, a
/// byte-for-byte snapshot round trip of an authored polyline, and a proof
/// that the queue folds in canonical application order regardless of storage
/// order.
/// </summary>
public sealed class OrderStateHashTests
{
    // -- Fixtures, copied field-for-field from MissionStateTests so the ----
    // -- pinned baseline below matches that file's BuildSampleState() -------
    // -- exactly under the pre-task-61 hasher. ------------------------------

    private static Mission BuildSampleMission(ulong seed = 12_345UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 999UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 50),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 1, OperatorCount: 4),
            new MissionFactionSetup(FactionId: 0, OperatorCount: 4)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildSampleOperator(int entityId) => new(
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

    /// <summary>
    /// Field-for-field identical to <c>MissionStateTests.BuildSampleState</c>,
    /// with an <see cref="MissionState.OrderQueue"/> left at its default
    /// (<see cref="OrderQueue.Empty"/>) and an empty
    /// <see cref="MissionState.OrderAssignments"/> — the "no order-layer
    /// activity yet" state the pinned baseline below was captured against.
    /// </summary>
    private static MissionState BuildSampleState() => new(
        Tick: 42,
        Phase: 1,
        Winner: -1,
        NextEntityId: 8,
        NextEventSequence: 3)
    {
        Operators = ImmutableArray.Create(BuildSampleOperator(1), BuildSampleOperator(2)),
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

    private static Order BuildSampleOrder(
        long orderId = 0,
        long orderSequence = 0,
        long targetTick = 5,
        int factionId = 0,
        OrderKind kind = OrderKind.MoveAlongPath,
        ImmutableArray<ulong> addressees = default,
        ImmutableArray<OrderPathNode> pathNodes = default) => new(
            orderId,
            orderSequence,
            targetTick,
            factionId,
            kind)
        {
            Addressees = addressees.IsDefault ? ImmutableArray.Create(1UL, 2UL) : addressees,
            PathNodes = pathNodes.IsDefault
            ? ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(10, 0), new OrderPathNode(10, 10))
            : pathNodes,
        };

    private static OrderQueue QueueOf(params Order[] orders) => OrderQueue.Empty with
    {
        NextOrderId = orders.Length,
        NextOrderSequence = orders.Length,
        Orders = ImmutableArray.Create(orders),
    };

    // -- Task 85 (docs/plans/2026-08-07-sandata-scaffold.md, wave-12 audit's
    // -- "Task 52's golden baseline against task 85's single-pin rule"):
    // -- the two tests below used to pin an absolute literal
    // -- (PreTask61BaselineHash) to guard a relational property — "an empty
    // -- order queue folds nothing" — and every legitimate change to the
    // -- operator fold (e.g. task 79c) broke the literal even though the
    // -- property it guarded never moved. Both are now comparisons between
    // -- two hashes computed live by today's hasher, so they can never go
    // -- stale from an unrelated fold change, and they still fail if the
    // -- property they guard is genuinely broken (see PR notes for the
    // -- break-and-revert proof). The one remaining absolute state-hash
    // -- literal in this test assembly is
    // -- MissionStateTests.PreTask79cBaselineHash, the deliberate canary.

    /// <summary>
    /// <see cref="SandataStateHasher"/>'s <c>FoldOrderQueue</c> is gated on
    /// <c>queue.Equals(OrderQueue.Empty)</c>, not on how that empty value was
    /// built. This proves the gate is genuinely value-based: a queue built by
    /// never touching <see cref="MissionState.OrderQueue"/> at all and a
    /// queue built by explicitly constructing zero orders through
    /// <see cref="QueueOf"/> are two different code paths to the same value,
    /// and both must hash identically because both equal
    /// <see cref="OrderQueue.Empty"/>.
    /// </summary>
    [Fact]
    public void StateHash_WithEmptyOrderQueueAndNoAssignments_IsUnaffectedByHowTheEmptyQueueWasConstructed()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var neverPopulatedState = BuildSampleState();
        var explicitlyEmptyState = BuildSampleState() with { OrderQueue = QueueOf() };

        Assert.Equal(OrderQueue.Empty, neverPopulatedState.OrderQueue);
        Assert.Equal(OrderQueue.Empty, explicitlyEmptyState.OrderQueue);
        Assert.Empty(neverPopulatedState.OrderAssignments);

        var neverPopulatedHash = SandataStateHasher.Compute(mission, neverPopulatedState, ruleset);
        var explicitlyEmptyHash = SandataStateHasher.Compute(mission, explicitlyEmptyState, ruleset);

        Assert.Equal(neverPopulatedHash, explicitlyEmptyHash);
    }

    /// <summary>
    /// The correctness edge case the empty-queue gate has to get right: a
    /// queue whose <see cref="OrderQueue.Orders"/> array is empty because
    /// every submission so far was rejected still has
    /// <see cref="OrderQueue.NextOrderId"/> and
    /// <see cref="OrderQueue.NextOrderSequence"/> advanced away from zero, so
    /// it is a genuinely different authoritative state from a mission that
    /// never submitted anything — <see cref="OrderQueue.Empty"/> — and must
    /// not collide with the never-populated baseline, computed live rather
    /// than pinned.
    /// </summary>
    [Fact]
    public void StateHash_QueueWithAdvancedCountersButNoStoredOrders_DiffersFromTheEmptyBaseline()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var neverPopulatedState = BuildSampleState();
        var advancedCountersState = BuildSampleState() with
        {
            OrderQueue = OrderQueue.Empty with { NextOrderId = 3, NextOrderSequence = 3 },
        };

        var neverPopulatedHash = SandataStateHasher.Compute(mission, neverPopulatedState, ruleset);
        var advancedCountersHash = SandataStateHasher.Compute(mission, advancedCountersState, ruleset);

        Assert.NotEqual(neverPopulatedHash, advancedCountersHash);
    }

    // -- The state hash moves when any Order field changes, one case per ---
    // -- field, changed alone. ------------------------------------------------

    [Fact]
    public void StateHash_Moves_WhenOrderIdChanges()
    {
        AssertHashMoves(
            BuildSampleOrder(orderId: 0),
            BuildSampleOrder(orderId: 1));
    }

    [Fact]
    public void StateHash_Moves_WhenOrderSequenceChanges()
    {
        AssertHashMoves(
            BuildSampleOrder(orderSequence: 0),
            BuildSampleOrder(orderSequence: 1));
    }

    [Fact]
    public void StateHash_Moves_WhenTargetTickChanges()
    {
        AssertHashMoves(
            BuildSampleOrder(targetTick: 5),
            BuildSampleOrder(targetTick: 6));
    }

    [Fact]
    public void StateHash_Moves_WhenFactionIdChanges()
    {
        AssertHashMoves(
            BuildSampleOrder(factionId: 0),
            BuildSampleOrder(factionId: 1));
    }

    [Fact]
    public void StateHash_Moves_WhenKindChanges()
    {
        AssertHashMoves(
            BuildSampleOrder(kind: OrderKind.MoveAlongPath),
            BuildSampleOrder(kind: OrderKind.Hold));
    }

    [Fact]
    public void StateHash_Moves_WhenAddresseesChange()
    {
        AssertHashMoves(
            BuildSampleOrder(addressees: ImmutableArray.Create(1UL, 2UL)),
            BuildSampleOrder(addressees: ImmutableArray.Create(1UL, 3UL)));
    }

    [Fact]
    public void StateHash_Moves_WhenPathNodesChange()
    {
        AssertHashMoves(
            BuildSampleOrder(pathNodes: ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(10, 0))),
            BuildSampleOrder(pathNodes: ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(11, 0))));
    }

    private static void AssertHashMoves(Order baseline, Order changed)
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var baselineState = BuildSampleState() with { OrderQueue = QueueOf(baseline) };
        var changedState = BuildSampleState() with { OrderQueue = QueueOf(changed) };

        var baselineHash = SandataStateHasher.Compute(mission, baselineState, ruleset);
        var changedHash = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baselineHash, changedHash);
    }

    // -- The state hash also moves for a per-operator OrderAssignment. -----

    [Fact]
    public void StateHash_Moves_WhenAnOrderAssignmentIsPresent()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var baselineState = BuildSampleState();
        var changedState = BuildSampleState() with
        {
            OrderAssignments = ImmutableArray.Create(new OrderAssignment(EntityId: 1, OrderId: 0, CurrentNodeIndex: 0)
            {
                PathNodes = ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(10, 0)),
            }),
        };

        var baselineHash = SandataStateHasher.Compute(mission, baselineState, ruleset);
        var changedHash = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baselineHash, changedHash);
    }

    [Fact]
    public void StateHash_Moves_WhenAnOrderAssignmentsCurrentNodeIndexChanges()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var pathNodes = ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(10, 0));

        var baselineState = BuildSampleState() with
        {
            OrderAssignments = ImmutableArray.Create(
                new OrderAssignment(EntityId: 1, OrderId: 0, CurrentNodeIndex: 0) { PathNodes = pathNodes }),
        };
        var changedState = BuildSampleState() with
        {
            OrderAssignments = ImmutableArray.Create(
                new OrderAssignment(EntityId: 1, OrderId: 0, CurrentNodeIndex: 1) { PathNodes = pathNodes }),
        };

        var baselineHash = SandataStateHasher.Compute(mission, baselineState, ruleset);
        var changedHash = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baselineHash, changedHash);
    }

    // -- The queue folds in ascending (TargetTick, OrderSequence), never in --
    // -- raw storage order. ---------------------------------------------------

    [Fact]
    public void StateHash_QueueFoldsInApplicationOrder_RegardlessOfStorageOrder()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var orderA = BuildSampleOrder(orderId: 0, orderSequence: 0, targetTick: 20, factionId: 0);
        var orderB = BuildSampleOrder(orderId: 1, orderSequence: 1, targetTick: 5, factionId: 1);
        var orderC = BuildSampleOrder(orderId: 2, orderSequence: 2, targetTick: 5, factionId: 0);

        // Application order (ascending TargetTick, then OrderSequence) is
        // B (5,1), C (5,2), A (20,0) -- deliberately not the storage order
        // in either variant below.
        var storedInsertionOrder = OrderQueue.Empty with
        {
            NextOrderId = 3,
            NextOrderSequence = 3,
            Orders = ImmutableArray.Create(orderA, orderB, orderC),
        };
        var storedShuffledOrder = OrderQueue.Empty with
        {
            NextOrderId = 3,
            NextOrderSequence = 3,
            Orders = ImmutableArray.Create(orderC, orderA, orderB),
        };

        var stateWithInsertionOrder = BuildSampleState() with { OrderQueue = storedInsertionOrder };
        var stateWithShuffledOrder = BuildSampleState() with { OrderQueue = storedShuffledOrder };

        var hashFromInsertionOrder = SandataStateHasher.Compute(mission, stateWithInsertionOrder, ruleset);
        var hashFromShuffledOrder = SandataStateHasher.Compute(mission, stateWithShuffledOrder, ruleset);

        Assert.Equal(hashFromInsertionOrder, hashFromShuffledOrder);
    }

    // -- The snapshot round-trips an authored polyline byte for byte, ------
    // -- both inside the queue and inside a per-operator assignment. -------

    [Fact]
    public void Snapshot_RoundTrips_OrderQueuePathNodes_ByteForByte()
    {
        var nodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(40, 0),
            new OrderPathNode(40, 80),
            new OrderPathNode(-20, 80));
        var order = BuildSampleOrder(pathNodes: nodes);
        var original = BuildSampleState() with { OrderQueue = QueueOf(order) };

        var restored = original.ToSnapshot().ToState();

        Assert.Equal(nodes, restored.OrderQueue.Orders[0].PathNodes);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Snapshot_RoundTrips_OrderAssignmentPathNodes_ByteForByte()
    {
        var nodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(15, 5),
            new OrderPathNode(30, 5),
            new OrderPathNode(30, 45),
            new OrderPathNode(60, 45));
        var assignment = new OrderAssignment(EntityId: 1, OrderId: 4, CurrentNodeIndex: 2) { PathNodes = nodes };
        var original = BuildSampleState() with { OrderAssignments = ImmutableArray.Create(assignment) };

        var restored = original.ToSnapshot().ToState();

        Assert.Equal(nodes, restored.OrderAssignments[0].PathNodes);
        Assert.Equal(original, restored);
    }

    /// <summary>
    /// The defect design section 16 names by name: "an authored path
    /// recomputed on resume is a defect that would let the nav bake state at
    /// load time rewrite a decision the player made an hour earlier." This
    /// builds a resumed assignment alongside a deliberately different
    /// polyline a hypothetical re-route would have produced for the same
    /// start and goal, and asserts the resumed value is the original nodes,
    /// never the alternative -- a wrong implementation that "recomputes and
    /// passes a naive round-trip test" would instead return something equal
    /// in shape (same count) but not in content, or would silently substitute
    /// the alternative.
    /// </summary>
    [Fact]
    public void Snapshot_ResumedAuthoredPolyline_IsTheOriginalNodes_NotARecomputedAlternative()
    {
        var originalNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(100, 0),
            new OrderPathNode(100, 100));
        var hypotheticalRerouteNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(50, 50),
            new OrderPathNode(100, 100));
        var assignment = new OrderAssignment(EntityId: 1, OrderId: 7, CurrentNodeIndex: 1)
        {
            PathNodes = originalNodes,
        };
        var original = BuildSampleState() with { OrderAssignments = ImmutableArray.Create(assignment) };

        var restored = original.ToSnapshot().ToState();

        Assert.Equal(originalNodes, restored.OrderAssignments[0].PathNodes);
        Assert.NotEqual(hypotheticalRerouteNodes, restored.OrderAssignments[0].PathNodes);
    }
}
