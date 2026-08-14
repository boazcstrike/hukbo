using System.Collections.Immutable;
using Sandata.Core.Events;
using Sandata.Core.Orders;

namespace Sandata.Core.Simulation;

/// <summary>
/// Sandata's save/resume DTO for <see cref="MissionState"/> — the field-for-
/// field mirror of everything design section 4 lists as authoritative, and
/// nothing design section 4 lists as derived. <see cref="MissionState.ToSnapshot"/>
/// and <see cref="ToState"/> are the two directions of the round trip;
/// design section 4's save-resume equivalence rule is that the state and
/// event hashes after a mid-mission snapshot and resume are identical to
/// what an unresumed run would have produced, which requires this type to
/// carry exactly the authoritative fields and none of the derived ones —
/// every derived structure (the nav grid, the clearance field, the A* scratch
/// state, published path polylines, line-of-sight and vision-cone results,
/// the collision grid, and the render snapshot) is rebuilt after resume, not
/// stored here.
/// </summary>
/// <remarks>
/// <see cref="OrderQueue"/> and <see cref="OrderAssignments"/> (task 61 of
/// Sandata's scaffold plan) follow that same rule in the
/// direction design section 16 states for the order layer specifically: "An
/// authored polyline is player input. It is stored verbatim in the snapshot
/// and folds into the state hash. It is never recomputed, never re-smoothed,
/// and never replaced by a search result. On resume it is restored exactly as
/// it was drawn." Every <see cref="OrderAssignment.PathNodes"/> element and
/// every <see cref="Orders.Order.PathNodes"/> element inside
/// <see cref="OrderQueue"/> round-trips through this type completely
/// unchanged — this file does no smoothing, no re-baking, and no
/// re-derivation of either array.
/// <para>
/// Task 72 of the same plan closed a second, unrelated door on
/// <see cref="Orders.OrderQueue.Orders"/> that let a caller inject orders
/// without going through <see cref="Orders.OrderQueue.SubmitValidated"/>.
/// <see cref="ToState"/> below rebuilds <see cref="MissionState.OrderQueue"/>
/// through <see cref="Orders.OrderQueue.RestoreForResume"/> — the named,
/// non-validating door that task added specifically for this call site — so
/// this file's own resume path is never the exception to "exactly two doors"
/// that a future reader would have to go looking for.
/// </para>
/// </remarks>
public sealed record MissionSnapshot(
    long Tick,
    int Phase,
    int Winner,
    ulong NextEntityId,
    long NextEventSequence)
{
    public ImmutableArray<OperatorState> Operators { get; init; } =
        ImmutableArray<OperatorState>.Empty;

    public ImmutableArray<FactionAlertState> FactionAlerts { get; init; } =
        ImmutableArray<FactionAlertState>.Empty;

    public ImmutableArray<DoorState> Doors { get; init; } =
        ImmutableArray<DoorState>.Empty;

    public ImmutableArray<GroupPathState> Groups { get; init; } =
        ImmutableArray<GroupPathState>.Empty;

    public ImmutableArray<RngStreamState> RngStreams { get; init; } =
        ImmutableArray<RngStreamState>.Empty;

    /// <summary>The authoritative order queue, stored verbatim. See this type's own remarks.</summary>
    public OrderQueue OrderQueue { get; init; } = OrderQueue.Empty;

    /// <summary>Every operator's current order assignment, stored verbatim. See this type's own remarks.</summary>
    public ImmutableArray<OrderAssignment> OrderAssignments { get; init; } =
        ImmutableArray<OrderAssignment>.Empty;

    /// <summary>
    /// Task 76's ordered mission event feed and running event hash, stored
    /// verbatim. Design section 4: the event hash is independent of the
    /// state hash, but is itself authoritative — a resumed mission must
    /// reproduce the same event hash an unresumed run would have produced,
    /// which requires carrying <see cref="MissionEventFeed.Hash"/> (never
    /// truncated by the 200-event retention cap) across the round trip
    /// rather than trying to recompute it from the capped
    /// <see cref="MissionEventFeed.Events"/> window alone.
    /// </summary>
    public MissionEventFeed EventFeed { get; init; } = MissionEventFeed.Empty;

    /// <summary>
    /// Stage 0's per-room clear state, stored verbatim — see
    /// <see cref="MissionState.RoomClearStates"/>'s own remarks. Authoritative
    /// and hashed, unlike the <see cref="Navigation.RoomLayout"/> it is
    /// evaluated against, which is derived and rebuilt after resume rather
    /// than carried here.
    /// </summary>
    public ImmutableArray<RoomClearState> RoomClearStates { get; init; } =
        ImmutableArray<RoomClearState>.Empty;

    /// <summary>
    /// Restores a <see cref="MissionState"/> from this snapshot. Every
    /// collection is copied by value (an <see cref="ImmutableArray{T}"/>
    /// assignment shares the same backing array, which is safe because both
    /// types treat the array as immutable), so the result compares equal to
    /// the original state under <see cref="MissionState.Equals(MissionState?)"/>
    /// without re-deriving anything. <see cref="MissionState.OrderQueue"/>
    /// is rebuilt through <see cref="Orders.OrderQueue.RestoreForResume"/>
    /// rather than assigned directly — see this type's own remarks for why
    /// that is the named, non-validating door this call site is required to
    /// use.
    /// </summary>
    public MissionState ToState() => new(Tick, Phase, Winner, NextEntityId, NextEventSequence)
    {
        Operators = Operators,
        FactionAlerts = FactionAlerts,
        Doors = Doors,
        Groups = Groups,
        RngStreams = RngStreams,
        OrderQueue = Orders.OrderQueue.RestoreForResume(OrderQueue.NextOrderId, OrderQueue.NextOrderSequence, OrderQueue.Orders),
        OrderAssignments = OrderAssignments,
        EventFeed = EventFeed,
        RoomClearStates = RoomClearStates,
    };
}

/// <summary>
/// The <see cref="MissionState"/> side of the round trip declared on
/// <see cref="MissionSnapshot"/>.
/// </summary>
public static class MissionStateSnapshotExtensions
{
    /// <summary>
    /// Captures this state's authoritative fields into a
    /// <see cref="MissionSnapshot"/>. See <see cref="MissionSnapshot"/>'s
    /// remarks for why this carries exactly the authoritative fields and
    /// nothing derived.
    /// </summary>
    public static MissionSnapshot ToSnapshot(this MissionState state) =>
        new(state.Tick, state.Phase, state.Winner, state.NextEntityId, state.NextEventSequence)
        {
            Operators = state.Operators,
            FactionAlerts = state.FactionAlerts,
            Doors = state.Doors,
            Groups = state.Groups,
            RngStreams = state.RngStreams,
            OrderQueue = state.OrderQueue,
            OrderAssignments = state.OrderAssignments,
            EventFeed = state.EventFeed,
            RoomClearStates = state.RoomClearStates,
        };
}
