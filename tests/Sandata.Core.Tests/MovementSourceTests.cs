using System.Collections.Immutable;
using Sandata.Core.Collision;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Squads;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 59 of docs/plans/2026-08-07-sandata-scaffold.md: <see cref="OrderAssignment"/>
/// and <see cref="MovementSource"/> against design section 16's per-tick
/// movement-source rule — "Every operator's movement, on every tick, comes
/// from exactly one of two sources... There is no third case and no blend of
/// the two" — and the four written conditions that clear an assignment.
/// </summary>
public sealed class MovementSourceTests
{
    private static readonly OrderPathNode StartNode = new(2, 2);
    private static readonly OrderPathNode EndNode = new(38, 2);

    private static OrderAssignment SampleAssignment(ulong entityId = 5, long orderId = 100) =>
        new(entityId, orderId, CurrentNodeIndex: 0)
        {
            PathNodes = ImmutableArray.Create(StartNode, EndNode),
        };

    /// <summary>
    /// A 10 by 1 cell grid (40 by 4 wu) with a single vertical door line at
    /// world X 16, cells (0,0) through (9,0) otherwise open — the same
    /// supercover geometry <c>NavBakeTests</c> uses for its own door fixture,
    /// scaled to one row. <paramref name="doorState"/> 1 is open (design
    /// section 12: 1 is open, 0 is closed); an open door's cells are baked
    /// <see cref="NavCellFlags.Open"/> by <see cref="NavBake.Bake"/>, and a
    /// closed door's cells are baked <see cref="NavCellFlags.Door"/>.
    /// </summary>
    private static NavGrid BuildGridWithDoor(int doorState)
    {
        var grid = new NavGrid(width: 10, height: 1);
        var door = new DoorRecord(LineNumber: 1, X1: 16, Y1: 0, X2: 16, Y2: 4, Hinge: 0, State: doorState);

        NavBake.Bake(grid, walls: [], doors: [door], bodyRadiusWu: 1);

        return grid;
    }

    // ------------------------------------------------------------------
    // "No third case": exactly one of two sources, exhaustively.
    // ------------------------------------------------------------------

    /// <summary>
    /// Exhaustive fixture over assignment-present x assignment-absent. Each
    /// call asserts the selected source is one of exactly the two known
    /// subtypes via a switch with no default case reachable except a thrown
    /// exception — a wrong implementation that introduced a third
    /// <see cref="MovementSource"/> subtype or returned <see langword="null"/>
    /// fails this test rather than silently passing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void For_SelectsExactlyOneOfTwoSources_NeverBothNeverNeither(bool assignmentPresent)
    {
        var assignment = assignmentPresent ? SampleAssignment() : null;

        var source = MovementSource.For(assignment);

        var selectedKind = source switch
        {
            MovementSource.AutonomousSlot => "autonomous-slot",
            MovementSource.AuthoredOrder => "authored-order",
            null => throw new InvalidOperationException(
                "MovementSource.For returned null -- neither of the two sources was selected."),
            _ => throw new InvalidOperationException(
                $"MovementSource.For returned an unrecognised subtype {source.GetType()} -- a third case exists."),
        };

        Assert.Equal(assignmentPresent ? "authored-order" : "autonomous-slot", selectedKind);
    }

    // ------------------------------------------------------------------
    // The four clearing conditions, each with its own reason code.
    // ------------------------------------------------------------------

    /// <summary>
    /// Each of the four conditions is exercised in isolation and each names a
    /// distinct <see cref="MovementSource.AssignmentClearReason"/>. Collected
    /// into a set and asserted to have four members — distinctness across all
    /// four, not merely that each differs from a zero/default value.
    /// </summary>
    [Fact]
    public void Evaluate_EachOfTheFourClearingConditions_NamesADistinctReasonCode()
    {
        var openGrid = BuildGridWithDoor(doorState: 1);
        var closedDoorGrid = BuildGridWithDoor(doorState: 0);
        var assignment = SampleAssignment();

        var reachedFinalNode = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: false, reachedFinalNode: true, openGrid);

        var cancelled = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: true, reachedFinalNode: false, openGrid);

        var died = MovementSource.Evaluate(
            assignment, operatorIsAlive: false, cancelOrderApplied: false, reachedFinalNode: false, openGrid);

        var untraversable = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: false, reachedFinalNode: false, closedDoorGrid);

        Assert.Equal(MovementSource.AssignmentClearReason.ReachedFinalNode, reachedFinalNode.ClearedReason);
        Assert.Equal(MovementSource.AssignmentClearReason.Cancelled, cancelled.ClearedReason);
        Assert.Equal(MovementSource.AssignmentClearReason.OperatorDied, died.ClearedReason);
        Assert.Equal(MovementSource.AssignmentClearReason.PolylineUntraversable, untraversable.ClearedReason);

        var distinctReasons = new HashSet<MovementSource.AssignmentClearReason>
        {
            reachedFinalNode.ClearedReason!.Value,
            cancelled.ClearedReason!.Value,
            died.ClearedReason!.Value,
            untraversable.ClearedReason!.Value,
        };

        Assert.Equal(4, distinctReasons.Count);

        // Every one of the four results also discards the assignment
        // entirely, not merely a flag beside it.
        Assert.Null(reachedFinalNode.Assignment);
        Assert.Null(cancelled.Assignment);
        Assert.Null(died.Assignment);
        Assert.Null(untraversable.Assignment);
    }

    /// <summary>
    /// When none of the four conditions holds, the assignment comes back
    /// unchanged and no reason is reported.
    /// </summary>
    [Fact]
    public void Evaluate_WhenNoConditionHolds_ReturnsTheAssignmentUnchangedWithNoReason()
    {
        var openGrid = BuildGridWithDoor(doorState: 1);
        var assignment = SampleAssignment();

        var result = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: false, reachedFinalNode: false, openGrid);

        Assert.Equal(assignment, result.Assignment);
        Assert.Null(result.ClearedReason);
    }

    // ------------------------------------------------------------------
    // A closed door clears; it never re-routes.
    // ------------------------------------------------------------------

    /// <summary>
    /// The polyline runs through world X 16, the door's own cell. With the
    /// door open the polyline is traversable and the assignment survives.
    /// With the door closed, <see cref="MovementSource.Evaluate"/> clears the
    /// assignment with <see cref="MovementSource.AssignmentClearReason.PolylineUntraversable"/>
    /// and returns <see langword="null"/> for the assignment itself — not a
    /// flag beside an unchanged or a re-routed polyline. An implementation
    /// that instead searched for a detour and returned a new
    /// <see cref="OrderAssignment"/> with different <see cref="OrderAssignment.PathNodes"/>
    /// would still fail <c>Assert.Null(result.Assignment)</c> below, exactly
    /// as it should: design section 16 states the polyline "is never silently
    /// repaired, re-routed, or re-smoothed."
    /// </summary>
    [Fact]
    public void Evaluate_AClosedDoorAcrossThePolyline_ClearsRatherThanReroutes()
    {
        var assignment = SampleAssignment();

        var openGrid = BuildGridWithDoor(doorState: 1);
        var whileOpen = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: false, reachedFinalNode: false, openGrid);

        Assert.Equal(assignment, whileOpen.Assignment);
        Assert.Null(whileOpen.ClearedReason);

        var closedDoorGrid = BuildGridWithDoor(doorState: 0);
        var afterClosed = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: false, reachedFinalNode: false, closedDoorGrid);

        Assert.Null(afterClosed.Assignment);
        Assert.Equal(MovementSource.AssignmentClearReason.PolylineUntraversable, afterClosed.ClearedReason);
    }

    /// <summary>
    /// <see cref="MovementSource.IsPolylineTraversable"/> directly, isolated
    /// from <see cref="MovementSource.Evaluate"/>'s other three conditions:
    /// the same polyline is traversable against the open-door bake and not
    /// traversable against the closed-door bake.
    /// </summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void IsPolylineTraversable_ReflectsTheDoorsCurrentBakedState(int doorState, bool expectedTraversable)
    {
        var grid = BuildGridWithDoor(doorState);
        var pathNodes = ImmutableArray.Create(StartNode, EndNode);

        var traversable = MovementSource.IsPolylineTraversable(pathNodes, grid);

        Assert.Equal(expectedTraversable, traversable);
    }

    // ------------------------------------------------------------------
    // Exclusion from slot targeting, and the same-tick resumption.
    // ------------------------------------------------------------------

    /// <summary>
    /// An operator holding an assignment is absent from
    /// <see cref="MovementSource.SlotTargetingRoster"/>'s output; the same
    /// operator, once its entry is removed from the assignments map, is
    /// present again. No tick counter appears anywhere in this test — the
    /// "same tick" property (design section 16: "autonomy resumes on the
    /// same tick") is demonstrated by the fact that nothing but the
    /// assignments map itself changes between the two calls: the roster
    /// consumes whatever assignments map is current, without any concept of
    /// "the tick has not advanced yet."
    /// </summary>
    [Fact]
    public void SlotTargetingRoster_ExcludesAnOrderedOperator_AndIncludesItAgainOnceCleared()
    {
        ulong[] roster = [1, 2, 3];
        var assignment = SampleAssignment(entityId: 2);

        ulong[] whileOrdered = [2];
        var stillUnderOrders = MovementSource.SlotTargetingRoster(roster, whileOrdered);
        Assert.Equal(new ulong[] { 1, 3 }, stillUnderOrders);

        var openGrid = BuildGridWithDoor(doorState: 1);
        var evaluation = MovementSource.Evaluate(
            assignment, operatorIsAlive: true, cancelOrderApplied: true, reachedFinalNode: false, openGrid);
        Assert.Null(evaluation.Assignment);

        // Simulate stage 1 applying the clearing this same tick, before the
        // slot-targeting stage runs later in the same tick's pipeline: the
        // cleared operator's entry id is removed from the very array handed
        // to SlotTargetingRoster, with no tick having advanced anywhere.
        ulong[] afterClearingSameTick = [];

        var resumedThisTick = MovementSource.SlotTargetingRoster(roster, afterClearingSameTick);
        Assert.Equal(new ulong[] { 1, 2, 3 }, resumedThisTick);
    }

    /// <summary>
    /// <see cref="MovementSource.SlotTargetingRoster"/> is permutation
    /// invariant in the order operator entity IDs are supplied: two shuffles
    /// of the same roster and the same assignments map produce the identical
    /// ascending result.
    /// </summary>
    [Fact]
    public void SlotTargetingRoster_PermutingOperatorOrder_ChangesNothing()
    {
        ulong[] ascending = [1, 2, 3, 4, 5];
        ulong[] shuffled = [4, 1, 5, 2, 3];
        ulong[] assignedEntityIds = [3];

        var fromAscending = MovementSource.SlotTargetingRoster(ascending, assignedEntityIds);
        var fromShuffled = MovementSource.SlotTargetingRoster(shuffled, assignedEntityIds);

        Assert.Equal(new ulong[] { 1, 2, 4, 5 }, fromAscending);

        // ImmutableArray<T>'s own Equals compares the backing array by
        // reference, not by content, so two independently-built results
        // must be compared as plain arrays here rather than as
        // ImmutableArray<ulong> values directly.
        Assert.Equal(fromAscending.ToArray(), fromShuffled.ToArray());
    }

    // ------------------------------------------------------------------
    // Squad grouping is unaffected by an assignment's presence.
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>SquadGrouping.Compute</c> takes no assignment of any kind as a
    /// parameter, so the group id it derives for a roster cannot depend on
    /// whether any operator in that roster currently holds an
    /// <see cref="OrderAssignment"/>. This test makes that fact concrete
    /// rather than definitional: it calls <c>SquadGrouping.Compute</c> once
    /// while entity 2 is recorded, in a side <see cref="OrderAssignment"/>
    /// map that is never passed to <c>SquadGrouping.Compute</c>, as under
    /// orders, and once with that map empty — the real, unordered run — and
    /// asserts entity 2's <see cref="SquadSlot.GroupId"/> is identical both
    /// times, while <see cref="MovementSource.SlotTargetingRoster"/>'s output
    /// for the same two scenarios differs, showing the two mechanisms are
    /// decoupled exactly as design section 16 states: "Squad grouping itself
    /// is unaffected... a group id still exists for every operator whether or
    /// not it is under orders."
    /// </summary>
    [Fact]
    public void AnOrderedOperatorsGroupId_EqualsTheGroupIdFromTheActualUnorderedRun()
    {
        ulong[] entityIds = [1, 2, 3];
        var isAlive = new[] { true, true, true };
        var factions = new[] { 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
        };

        ulong[] orderedAssignedEntityIds = [2];
        ulong[] unorderedAssignedEntityIds = [];

        // SquadGrouping.Compute now always takes positions and a cohesion
        // radius. This fact has never had positions of its own to give —
        // it exists to prove grouping is decoupled from order assignment,
        // not to say anything about distance — so both calls below place
        // every entity at the same coincident position (0, 0) and pass the
        // same generous radius. With every position identical, the squared
        // distance between any pair is always zero, so any non-negative
        // radius unions every same-faction candidate pair regardless of its
        // value; using the same radius and positions on both calls is what
        // keeps the ordered/unordered comparison below meaningful, since
        // that comparison must isolate the presence of an OrderAssignment
        // as the only variable.
        int[] coincidentPositions = [0, 0, 0];
        const int unconditionalUnionRadiusRaw = 1_000_000;

        var orderedResults = new SquadSlot[entityIds.Length];
        SquadGrouping.Compute(
            entityIds, isAlive, factions,
            coincidentPositions, coincidentPositions, unconditionalUnionRadiusRaw,
            pairs, orderedResults);

        var unorderedResults = new SquadSlot[entityIds.Length];
        SquadGrouping.Compute(
            entityIds, isAlive, factions,
            coincidentPositions, coincidentPositions, unconditionalUnionRadiusRaw,
            pairs, unorderedResults);

        var orderedGroupIdForEntityTwo = orderedResults[1].GroupId;
        var unorderedGroupIdForEntityTwo = unorderedResults[1].GroupId;

        Assert.Equal(unorderedGroupIdForEntityTwo, orderedGroupIdForEntityTwo);
        Assert.Equal(1UL, orderedGroupIdForEntityTwo);

        var orderedSlotTargetingRoster = MovementSource.SlotTargetingRoster(entityIds, orderedAssignedEntityIds);
        var unorderedSlotTargetingRoster = MovementSource.SlotTargetingRoster(entityIds, unorderedAssignedEntityIds);

        Assert.DoesNotContain(2UL, orderedSlotTargetingRoster);
        Assert.Contains(2UL, unorderedSlotTargetingRoster);
    }
}
