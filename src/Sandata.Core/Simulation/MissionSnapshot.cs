using System.Collections.Immutable;

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

    /// <summary>
    /// Restores a <see cref="MissionState"/> from this snapshot. Every
    /// collection is copied by value (an <see cref="ImmutableArray{T}"/>
    /// assignment shares the same backing array, which is safe because both
    /// types treat the array as immutable), so the result compares equal to
    /// the original state under <see cref="MissionState.Equals(MissionState?)"/>
    /// without re-deriving anything.
    /// </summary>
    public MissionState ToState() => new(Tick, Phase, Winner, NextEntityId, NextEventSequence)
    {
        Operators = Operators,
        FactionAlerts = FactionAlerts,
        Doors = Doors,
        Groups = Groups,
        RngStreams = RngStreams,
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
        };
}
