namespace Sandata.Core.Squads;

/// <summary>
/// One operator's derived squad-grouping result for the tick that produced
/// it, as computed by <see cref="SquadGrouping.Compute"/>. Every field here
/// is a pure function of that tick's entity IDs, living status, factions, and
/// candidate contact pairs — design section 8 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Grouping is
/// derived, not stored." None of it is written to
/// <c>Sandata.Core.Simulation.MissionState</c>, snapshotted, or folded into a
/// state hash; recomputing it from scratch every tick is what lets it survive
/// save and resume with no extra state and re-derive a leader the instant the
/// previous one dies, with no leaderless tick in between.
/// </summary>
/// <param name="EntityId">The operator this result describes.</param>
/// <param name="GroupId">
/// The identity of the squad <paramref name="EntityId"/> belongs to: the
/// minimum entity ID that has ever been unioned into its connected
/// component, whether or not that entity is currently alive. Unlike
/// <paramref name="LeaderEntityId"/>, this value does not move when the
/// component's lowest-ID member dies, because a dead member remains part of
/// the component for as long as it keeps appearing in the candidate pair
/// list that produced the component in the first place.
/// </param>
/// <param name="LeaderEntityId">
/// The lowest living entity ID in <paramref name="EntityId"/>'s component, or
/// <see langword="null"/> when every member of the component is dead. This
/// value is recomputed on every call, so a member's death re-derives the
/// leader on the very same call — there is no intermediate tick in which the
/// squad has no leader.
/// </param>
/// <param name="SlotIndex">
/// This operator's zero-based marching-order position within its squad,
/// assigned by ascending entity ID counting only the living members of the
/// component, or <see langword="null"/> when <paramref name="EntityId"/> is
/// not alive. A dead member holds no slot, so on the next call the remaining
/// living members' indices close the gap — design section 8's "a death
/// re-packs the slots deterministically on the next tick."
/// </param>
internal readonly record struct SquadSlot(
    ulong EntityId,
    ulong GroupId,
    ulong? LeaderEntityId,
    int? SlotIndex);
